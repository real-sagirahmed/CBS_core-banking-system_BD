using System.Security.Cryptography;
using System.Text;
using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers.Commands;

// ARCHITECTURAL INTENT: Coordinates Corporate Entity Onboarding, AES-256 Specimen Encryption,
// Multi-Signatory Mandate Resolution, Corporate Group Exposure Aggregation, and 8-Factor e-KYC
// in strict adherence to Bangladesh Bank BRPD Circular 12/2024 Module 8.
public class CrmCommandHandler
{
    private static readonly byte[] SignatureAesKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("CBS_CORE_BANKING_BD_CRM_SIGNATURE_AES256_SECRET_KEY"));

    private readonly ICrmRepository _crmRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILoanAccountRepository _loanRepository;
    private readonly ITradeFinanceRepository _tradeFinanceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CrmCommandHandler(
        ICrmRepository crmRepository,
        ICustomerRepository customerRepository,
        IAccountRepository accountRepository,
        ILoanAccountRepository loanRepository,
        ITradeFinanceRepository tradeFinanceRepository,
        IUnitOfWork unitOfWork)
    {
        _crmRepository = crmRepository ?? throw new ArgumentNullException(nameof(crmRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _loanRepository = loanRepository ?? throw new ArgumentNullException(nameof(loanRepository));
        _tradeFinanceRepository = tradeFinanceRepository ?? throw new ArgumentNullException(nameof(tradeFinanceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SpecimenSignatureResponse> RegisterSignatureAsync(RegisterSpecimenSignatureRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(request.CustomerCif);
        var signature = new SpecimenSignature(
            Guid.NewGuid(),
            cif,
            request.SignatoryName,
            request.Designation,
            request.NationalIdOrPassport,
            request.Type,
            request.RawSignatureDataOrBase64,
            request.CreatedByUserId,
            request.WorkstationIp ?? "127.0.0.1",
            DateTime.UtcNow,
            SignatureAesKey);

        await _crmRepository.AddSignatureAsync(signature, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapSignatureToResponse(signature);
    }

    public async Task<IReadOnlyList<SpecimenSignatureResponse>> GetSignaturesForAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var accNo = AccountNumber.From(accountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Account '{accountNumber}' was not found.");

        var mandate = await _crmRepository.GetMandateByAccountNumberAsync(accNo, ct);
        var result = new List<SpecimenSignature>();

        if (mandate != null)
        {
            foreach (var sigRule in mandate.Signatories)
            {
                var sig = await _crmRepository.GetSignatureByIdAsync(sigRule.SignatoryId, ct);
                if (sig != null) result.Add(sig);
            }
        }

        if (result.Count == 0)
        {
            var customerSigs = await _crmRepository.GetSignaturesByCifAsync(account.Cif, ct);
            result.AddRange(customerSigs);
        }

        return result.Select(MapSignatureToResponse).ToList();
    }

    public async Task<MandateResolutionResponse> DefineMandateAsync(DefineAccountMandateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var accNo = AccountNumber.From(request.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"Account '{request.AccountNumber}' was not found.");

        var sigRules = request.Signatories.Select(s =>
            new MandateSignatoryRule(s.SignatoryId, s.SignatoryName, s.Role, s.IsMandatorySignatory)).ToList();

        var existingMandate = await _crmRepository.GetMandateByAccountNumberAsync(accNo, ct);
        if (existingMandate != null)
        {
            existingMandate.Deactivate(request.ApprovedByUserId);
            await _crmRepository.UpdateMandateAsync(existingMandate, ct);
        }

        var mandate = new AccountSignatoryMandate(
            Guid.NewGuid(),
            accNo,
            request.OperationalRule,
            request.SingleSignatoryThresholdBdt,
            sigRules,
            request.ApprovedByUserId,
            DateTime.UtcNow);

        await _crmRepository.AddMandateAsync(mandate, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MandateResolutionResponse(
            accNo.Value,
            true,
            mandate.OperationalRule.ToString(),
            "Mandate successfully registered and activated.");
    }

    public async Task<MandateResolutionResponse> ResolveMandateAsync(ResolveMandateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var accNo = AccountNumber.From(request.AccountNumber);
        var mandate = await _crmRepository.GetMandateByAccountNumberAsync(accNo, ct)
            ?? throw new KeyNotFoundException($"No operational mandate configured for account '{request.AccountNumber}'.");

        var (isAuthorized, reason) = mandate.EvaluateMandate(request.PresentingSignatoryIds, request.TransactionAmountBdt);

        return new MandateResolutionResponse(
            accNo.Value,
            isAuthorized,
            mandate.OperationalRule.ToString(),
            reason);
    }

    public async Task<CorporateCustomerResponse> CreateCorporateCustomerAsync(CreateCorporateCustomerRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(request.CifNumber);
        var entity = new CorporateCustomerEntity(
            cif,
            request.TradeName,
            request.LegalForm,
            request.RegistrationNumber,
            request.IncorporationDate,
            request.TaxIdentificationNumber,
            request.SectorCode,
            Money.Bdt(request.AuthorizedCapitalBdt),
            Money.Bdt(request.PaidUpCapitalBdt),
            Money.Bdt(request.AnnualTurnoverBdt),
            request.EmployeeCount,
            request.MakerUserId,
            DateTime.UtcNow,
            request.CorporateGroupId);

        await _crmRepository.AddCorporateEntityAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapCorporateEntityToResponse(entity);
    }

    public async Task<CorporateCustomerResponse> AddStakeholderAsync(string cifNumber, AddCorporateStakeholderRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(cifNumber);
        var entity = await _crmRepository.GetCorporateEntityByCifAsync(cif, ct)
            ?? throw new KeyNotFoundException($"Corporate entity with CIF '{cifNumber}' was not found.");

        var stakeholder = new CorporateStakeholder(
            Guid.NewGuid(),
            request.FullName,
            request.Role,
            request.NationalIdOrPassport,
            request.TaxId,
            request.ShareholdingPercentage,
            request.IsPep,
            request.SanctionStatus ?? SanctionStatus.Clean);

        entity.AddStakeholder(stakeholder);

        await _crmRepository.UpdateCorporateEntityAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapCorporateEntityToResponse(entity);
    }

    public async Task<CorporateCustomerResponse> ActivateCorporateCustomerAsync(string cifNumber, string checkerUserId, CancellationToken ct = default)
    {
        var cif = new CifNumber(cifNumber);
        var entity = await _crmRepository.GetCorporateEntityByCifAsync(cif, ct)
            ?? throw new KeyNotFoundException($"Corporate entity with CIF '{cifNumber}' was not found.");

        entity.Activate(checkerUserId, DateTime.UtcNow);

        await _crmRepository.UpdateCorporateEntityAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapCorporateEntityToResponse(entity);
    }

    public async Task<CorporateGroup> CreateCorporateGroupAsync(CreateCorporateGroupRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var group = new CorporateGroup(
            request.GroupId,
            request.GroupName,
            request.StatutoryGroupLimitBdt,
            request.CreatedByUserId,
            DateTime.UtcNow);

        await _crmRepository.AddCorporateGroupAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return group;
    }

    public async Task<CorporateGroup> LinkCorporateGroupMemberAsync(string groupId, LinkGroupMemberRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        var cif = new CifNumber(request.MemberCif);
        group.LinkMemberCif(cif);

        await _crmRepository.UpdateCorporateGroupAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return group;
    }

    public async Task<CorporateGroupExposureResponse> GetCorporateGroupExposureAsync(string groupId, CancellationToken ct = default)
    {
        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        decimal totalFunded = 0m;
        decimal totalNonFunded = 0m;

        foreach (string cifStr in group.MemberCifNumbers)
        {
            var memberCif = new CifNumber(cifStr);

            // Funded Loans Exposure
            var loans = await _loanRepository.GetByCifAsync(memberCif, ct);
            if (loans != null)
            {
                totalFunded += loans
                    .Where(l => l.DisbursedDate != null && l.OutstandingPrincipal.Amount > 0)
                    .Sum(l => l.OutstandingPrincipal.Amount);
            }

            // Non-Funded Trade Finance Exposure (LCs)
            var allLcs = await _tradeFinanceRepository.GetAllLcsAsync(ct);
            if (allLcs != null)
            {
                var accounts = await _accountRepository.GetByCifAsync(memberCif, ct);
                var accNumbers = accounts.Select(a => a.Id.Value).ToHashSet();

                totalNonFunded += allLcs
                    .Where(l => (l.Status == LcStatus.Issued || l.Status == LcStatus.BillLodged) &&
                                l.CustomerDepositAccountNumber != null &&
                                accNumbers.Contains(l.CustomerDepositAccountNumber))
                    .Sum(l => l.Amount.Amount);
            }
        }

        decimal totalExposure = totalFunded + totalNonFunded;
        decimal remainingLimit = group.StatutoryGroupLimitBdt - totalExposure;
        bool isBreached = totalExposure > group.StatutoryGroupLimitBdt;

        return new CorporateGroupExposureResponse(
            group.Id,
            group.GroupName,
            group.StatutoryGroupLimitBdt,
            group.MemberCifNumbers,
            totalFunded,
            totalNonFunded,
            totalExposure,
            remainingLimit,
            isBreached);
    }

    public async Task<EkycRiskResponse> EvaluateEkycRiskAsync(EvaluateEkycRiskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(request.CustomerCif);
        var score = new EkycRiskScore(
            request.OnboardingChannelScore,
            request.GeographicRiskScore,
            request.PepRiskScore,
            request.ProfessionSectorRiskScore,
            request.ProductRiskScore,
            request.TransactionalVolumeScore,
            request.AdverseHistoryScore,
            request.EntityLegalFormScore);

        var customer = await _customerRepository.GetByCifAsync(cif, ct);
        if (customer != null)
        {
            // Update customer risk status if present
            await _customerRepository.UpdateAsync(customer, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new EkycRiskResponse(
            cif.Value,
            score.CompositeScore,
            score.RiskLevel.ToString(),
            score.IsEddRequired);
    }

    public async Task<CustomerDocumentResponse> RegisterDocumentAsync(RegisterCustomerDocumentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var cif = new CifNumber(request.CustomerCif);
        var doc = new CustomerDocumentRecord(
            Guid.NewGuid(),
            cif,
            request.Category,
            request.DocumentNumber,
            request.IssueDate,
            request.ExpiryDate,
            request.IssuingAuthority,
            DateTime.UtcNow);

        await _crmRepository.AddDocumentAsync(doc, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapDocumentToResponse(doc, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    public async Task<IReadOnlyList<CustomerDocumentResponse>> GetExpiringDocumentsAsync(int thresholdDays, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var docs = await _crmRepository.GetExpiringDocumentsAsync(today, thresholdDays, ct);
        return docs.Select(d => MapDocumentToResponse(d, today)).ToList();
    }

    private static SpecimenSignatureResponse MapSignatureToResponse(SpecimenSignature sig)
    {
        return new SpecimenSignatureResponse(
            sig.Id,
            sig.CustomerCif.Value,
            sig.SignatoryName,
            sig.Designation,
            sig.NationalIdOrPassport,
            sig.Type.ToString(),
            sig.IntegrityChecksumSha256,
            sig.Status.ToString(),
            sig.CreatedByUserId,
            sig.WorkstationIp,
            sig.CreatedAtUtc);
    }

    private static CorporateCustomerResponse MapCorporateEntityToResponse(CorporateCustomerEntity entity)
    {
        var stakeholders = entity.Stakeholders.Select(s => new CorporateStakeholderDto(
            s.StakeholderId,
            s.FullName,
            s.Role.ToString(),
            s.NationalIdOrPassport,
            s.TaxId,
            s.ShareholdingPercentage,
            s.IsPep,
            s.SanctionStatus.ToString(),
            s.IsSignificantBeneficialOwner)).ToList();

        return new CorporateCustomerResponse(
            entity.Id.Value,
            entity.TradeName,
            entity.LegalForm.ToString(),
            entity.RegistrationNumber,
            entity.IncorporationDate,
            entity.TaxIdentificationNumber,
            entity.SectorCode,
            entity.AuthorizedCapital.Amount,
            entity.PaidUpCapital.Amount,
            entity.AnnualTurnover.Amount,
            entity.EmployeeCount,
            entity.CorporateGroupId,
            entity.Status.ToString(),
            stakeholders);
    }

    private static CustomerDocumentResponse MapDocumentToResponse(CustomerDocumentRecord doc, DateOnly asOfDate)
    {
        return new CustomerDocumentResponse(
            doc.Id,
            doc.CustomerCif.Value,
            doc.Category.ToString(),
            doc.DocumentNumber,
            doc.IssueDate,
            doc.ExpiryDate,
            doc.IssuingAuthority,
            doc.IsVerified,
            doc.CheckExpiryStatus(asOfDate).ToString());
    }
}
