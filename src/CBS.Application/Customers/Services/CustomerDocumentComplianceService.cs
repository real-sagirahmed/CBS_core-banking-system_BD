using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers.Services;

// ARCHITECTURAL INTENT: Institutional & Individual Customer Mandatory Document Expiration Engine,
// Automated Debit Block Restraints past 30-Day Grace Period, Dual-Control Renewal Unblocking,
// and FATCA Indicia Assessment strictly per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 8.1.8-d, 8.1.15, and 1.6.6.
public class CustomerDocumentComplianceService : ICustomerDocumentComplianceService
{
    private readonly ICustomerDocumentRecordRepository _documentRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountRestraintRepository _restraintRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerDocumentComplianceService(
        ICustomerDocumentRecordRepository documentRepository,
        IAccountRepository accountRepository,
        IAccountRestraintRepository restraintRepository,
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _restraintRepository = restraintRepository ?? throw new ArgumentNullException(nameof(restraintRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ExpiredDocumentScanSummaryDto> ScanAndApplyExpiredDocumentRestraintsAsync(
        ScanExpiredDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var expiredDocs = await _documentRepository.GetExpiredComplianceDocumentsAsync(request.AsOfDate, cancellationToken);
        var details = new List<ExpiredDocumentItemDto>();

        int expiredCount = 0;
        int gracePeriodBreachedCount = 0;
        int restraintsAppliedCount = 0;

        foreach (var doc in expiredDocs)
        {
            expiredCount++;
            int daysOverdue = request.AsOfDate.DayNumber - (doc.ExpiryDate?.DayNumber ?? request.AsOfDate.DayNumber);
            bool isBeyondGrace = doc.IsBeyondGracePeriod(request.AsOfDate);
            bool restraintApplied = false;

            if (isBeyondGrace)
            {
                gracePeriodBreachedCount++;

                // Retrieve all deposit accounts for the customer
                var accounts = await _accountRepository.GetByCifAsync(doc.CustomerCif, cancellationToken);
                foreach (var account in accounts)
                {
                    if (account.Status == AccountOperatingStatus.Closed)
                        continue;

                    var activeRestraints = await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(account.Id, cancellationToken);
                    bool alreadyBlocked = activeRestraints.Any(r => r.RestraintType == RestraintType.StatutoryDocumentExpired && r.Status == RestraintStatus.Active);

                    if (!alreadyBlocked)
                    {
                        var restraint = new AccountRestraint(
                            id: Guid.NewGuid().ToString("N"),
                            accountNumber: account.Id,
                            restraintType: RestraintType.StatutoryDocumentExpired,
                            scope: RestraintScope.DebitBlocked,
                            holdAmount: Money.Zero(account.Balance.Currency),
                            reasonCategory: "StatutoryDocumentExpired",
                            narrativeReason: $"Mandatory document '{doc.Category}' (#{doc.DocumentNumber}) expired on {doc.ExpiryDate:yyyy-MM-dd}, exceeding {doc.GracePeriodDays}-day grace period.",
                            issuingAuthority: doc.IssuingAuthority,
                            referenceCaseNo: $"DOC-EXP-{doc.DocumentNumber}",
                            placedByUserId: request.EvaluatorUserId,
                            authorizedByUserId: "SYSTEM_COMPLIANCE_AUTO",
                            createdAtUtc: DateTime.UtcNow);

                        await _restraintRepository.AddAsync(restraint, cancellationToken);
                        account.SetOperatingStatus(AccountOperatingStatus.BlockedDebit);
                        await _accountRepository.UpdateAsync(account, cancellationToken);

                        restraintsAppliedCount++;
                        restraintApplied = true;
                    }
                }
            }

            details.Add(new ExpiredDocumentItemDto(
                DocumentId: doc.Id,
                CustomerCif: doc.CustomerCif.Value,
                Category: doc.Category.ToString(),
                DocumentNumber: doc.DocumentNumber,
                ExpiryDate: doc.ExpiryDate ?? request.AsOfDate,
                DaysOverdue: Math.Max(0, daysOverdue),
                RestraintApplied: restraintApplied));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ExpiredDocumentScanSummaryDto(
            AsOfDate: request.AsOfDate,
            TotalScanned: expiredDocs.Count,
            ExpiredCount: expiredCount,
            GracePeriodBreachedCount: gracePeriodBreachedCount,
            RestraintsAppliedCount: restraintsAppliedCount,
            Details: details);
    }

    public async Task<RenewDocumentResponseDto> RenewDocumentAndReleaseRestraintsAsync(
        Guid documentId,
        RenewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        // INVARIANT 4: Maker-Checker Segregation on Document Renewal & Unblocking (BRPD 1.3.18, 10.3.5)
        if (string.Equals(request.MakerUserId.Trim(), request.CheckerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{request.MakerUserId}' cannot act as Checker to verify renewed document and unblock accounts.");
        }

        var doc = await _documentRepository.GetByIdAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer document with ID '{documentId}' was not found.");

        doc.RenewDocument(request.NewDocumentNumber, request.NewIssueDate, request.NewExpiryDate, request.MakerUserId);
        doc.Verify(request.CheckerUserId, DateTime.UtcNow);
        await _documentRepository.UpdateAsync(doc, cancellationToken);

        // Find customer's accounts and release StatutoryDocumentExpired restraints
        var accounts = await _accountRepository.GetByCifAsync(doc.CustomerCif, cancellationToken);
        bool anyReleased = false;

        foreach (var account in accounts)
        {
            var activeRestraints = await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(account.Id, cancellationToken);
            var expiredRestraints = activeRestraints.Where(r => r.RestraintType == RestraintType.StatutoryDocumentExpired && r.Status == RestraintStatus.Active).ToList();

            foreach (var r in expiredRestraints)
            {
                r.Release(request.MakerUserId, request.CheckerUserId, "Mandatory document renewed and verified under dual control.", DateTime.UtcNow);
                await _restraintRepository.UpdateAsync(r, cancellationToken);
                anyReleased = true;
            }

            // If account has no other debit-blocking restraints, restore to Active
            var remainingRestraints = await _restraintRepository.GetActiveRestraintsByAccountNumberAsync(account.Id, cancellationToken);
            bool hasOtherBlocks = remainingRestraints.Any(r =>
                r.Status == RestraintStatus.Active &&
                (r.Scope == RestraintScope.DebitBlocked || r.Scope == RestraintScope.TotalFreeze));

            if (!hasOtherBlocks && account.Status == AccountOperatingStatus.BlockedDebit)
            {
                account.SetOperatingStatus(AccountOperatingStatus.Active);
                await _accountRepository.UpdateAsync(account, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RenewDocumentResponseDto(
            DocumentId: doc.Id,
            CustomerCif: doc.CustomerCif.Value,
            DocumentNumber: doc.DocumentNumber,
            ExpiryDate: doc.ExpiryDate ?? request.NewExpiryDate,
            IsRestraintReleased: anyReleased,
            Message: anyReleased
                ? "Document renewed, verified, and account debit restraints were successfully released."
                : "Document renewed and verified successfully.");
    }

    public async Task<FatcaAssessmentDto> EvaluateFatcaComplianceAsync(
        FatcaComplianceEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var indiciaList = new List<string>();

        if (request.HasUsCitizenship) indiciaList.Add("US_CITIZENSHIP");
        if (request.HasUsGreenCard) indiciaList.Add("US_GREEN_CARD");
        if (request.BornInUs) indiciaList.Add("US_BIRTHPLACE");
        if (!string.IsNullOrWhiteSpace(request.UsAddress)) indiciaList.Add("US_MAILING_ADDRESS");
        if (!string.IsNullOrWhiteSpace(request.UsPhone)) indiciaList.Add("US_PHONE_NUMBER");
        if (request.HasStandingOrderToUsAccount) indiciaList.Add("US_STANDING_TRANSFER_INSTRUCTION");
        if (request.HasUsPowerOfAttorney) indiciaList.Add("US_POWER_OF_ATTORNEY");

        bool isUsPerson = indiciaList.Count > 0;
        FatcaClassification classification;
        if (request.HasUsCitizenship || request.BornInUs)
            classification = FatcaClassification.UsCitizen;
        else if (request.HasUsGreenCard)
            classification = FatcaClassification.UsGreenCardHolder;
        else if (isUsPerson)
            classification = FatcaClassification.UsResident;
        else
            classification = FatcaClassification.NonUsPerson;

        // If customer exists, update status
        var cif = new CifNumber(request.CustomerCif);
        var customer = await _customerRepository.GetByCifAsync(cif, cancellationToken);
        if (customer != null)
        {
            // Update customer if needed
            await _customerRepository.UpdateAsync(customer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new FatcaAssessmentDto(
            CustomerCif: request.CustomerCif,
            Classification: classification.ToString(),
            IsReportableUnderFatca: isUsPerson,
            IndiciaMatched: indiciaList,
            W8BenOrW9Reference: request.W8BenOrW9Reference,
            GiinNumber: request.GiinNumber,
            EvaluatedAtUtc: DateTime.UtcNow);
    }
}
