using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Crm.DTOs;
using CBS.Application.Customers;
using CBS.Domain.Adc;
using CBS.Domain.Customers;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Crm.Services;

// ARCHITECTURAL INTENT: Unified CRM Corporate Group, UBO, TP & ADC Central Reconciliation Service (BRPD Circular 12/2024 Modules 7 & 8).
// Coordinates corporate group exposure aggregation, beneficial ownership registration (>= 25%),
// transaction profile variance evaluation (> 150%), and automated balanced double-entry ADC settlement rectification.

public sealed class CrmAndAdcReconciliationService : ICrmAndAdcReconciliationService
{
    private readonly ICrmRepository _crmRepository;
    private readonly ICustomerTransactionProfileRepository _tpRepository;
    private readonly IAdcReconciliationRepository _adcReconciliationRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ILoanAccountRepository _loanRepository;
    private readonly ITradeFinanceRepository _tradeFinanceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CrmAndAdcReconciliationService(
        ICrmRepository crmRepository,
        ICustomerTransactionProfileRepository tpRepository,
        IAdcReconciliationRepository adcReconciliationRepository,
        IAccountRepository accountRepository,
        ILoanAccountRepository loanRepository,
        ITradeFinanceRepository tradeFinanceRepository,
        IUnitOfWork unitOfWork)
    {
        _crmRepository = crmRepository ?? throw new ArgumentNullException(nameof(crmRepository));
        _tpRepository = tpRepository ?? throw new ArgumentNullException(nameof(tpRepository));
        _adcReconciliationRepository = adcReconciliationRepository ?? throw new ArgumentNullException(nameof(adcReconciliationRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _loanRepository = loanRepository ?? throw new ArgumentNullException(nameof(loanRepository));
        _tradeFinanceRepository = tradeFinanceRepository ?? throw new ArgumentNullException(nameof(tradeFinanceRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    // =========================================================================
    // 1. Corporate Group & UBO Hierarchy
    // =========================================================================

    public async Task<CorporateGroupDetailResponse> AddGroupMemberDetailAsync(
        string groupId,
        AddGroupMemberDetailRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        var memberDetail = new GroupMemberDetail(
            request.CifNumber,
            request.EntityName,
            request.Role,
            request.ShareholdingPercentage,
            request.RegistrationNumber,
            DateTime.UtcNow);

        group.AddMemberDetail(memberDetail);

        await _crmRepository.UpdateCorporateGroupAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildGroupDetailResponseAsync(group, ct);
    }

    public async Task<CorporateGroupDetailResponse> RegisterBeneficialOwnerAsync(
        string groupId,
        RegisterBeneficialOwnerRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        var ubo = new BeneficialOwnerRecord(
            Guid.NewGuid(),
            request.FullName,
            request.NationalId,
            request.OwnerType,
            request.ShareholdingPercentage,
            request.IsPepOrIp,
            request.VerificationStatus ?? "Verified",
            DateTime.UtcNow);

        group.RegisterBeneficialOwner(ubo);

        await _crmRepository.UpdateCorporateGroupAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildGroupDetailResponseAsync(group, ct);
    }

    public async Task<CorporateGroupDetailResponse> GetGroupHierarchyAsync(string groupId, CancellationToken ct = default)
    {
        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        return await BuildGroupDetailResponseAsync(group, ct);
    }

    public async Task<bool> ValidateGroupExposureLimitAsync(
        string groupId,
        ValidateGroupExposureRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var group = await _crmRepository.GetCorporateGroupByIdAsync(groupId, ct)
            ?? throw new KeyNotFoundException($"Corporate group '{groupId}' was not found.");

        var (totalFunded, totalNonFunded) = await ComputeAggregatedGroupExposureAsync(group, ct);
        group.ValidateProposedExposure(totalFunded, totalNonFunded, request.ProposedNewFacilityBdt);

        return true;
    }

    private async Task<(decimal Funded, decimal NonFunded)> ComputeAggregatedGroupExposureAsync(CorporateGroup group, CancellationToken ct)
    {
        decimal totalFunded = 0m;
        decimal totalNonFunded = 0m;

        foreach (string cifStr in group.MemberCifNumbers)
        {
            var memberCif = new CifNumber(cifStr);

            // Funded Exposure (Loans)
            var loans = await _loanRepository.GetByCifAsync(memberCif, ct);
            if (loans != null)
            {
                totalFunded += loans
                    .Where(l => l.DisbursedDate != null && l.OutstandingPrincipal.Amount > 0)
                    .Sum(l => l.OutstandingPrincipal.Amount);
            }

            // Non-Funded Exposure (LCs)
            var allLcs = await _tradeFinanceRepository.GetAllLcsAsync(ct);
            if (allLcs != null)
            {
                var accounts = await _accountRepository.GetByCifAsync(memberCif, ct);
                if (accounts != null && accounts.Any())
                {
                    var accNumbers = accounts.Select(a => a.Id.Value).ToHashSet();
                    totalNonFunded += allLcs
                        .Where(l => (l.Status == LcStatus.Issued || l.Status == LcStatus.BillLodged) &&
                                    l.CustomerDepositAccountNumber != null &&
                                    accNumbers.Contains(l.CustomerDepositAccountNumber))
                        .Sum(l => l.Amount.Amount);
                }
            }
        }

        return (totalFunded, totalNonFunded);
    }

    private async Task<CorporateGroupDetailResponse> BuildGroupDetailResponseAsync(CorporateGroup group, CancellationToken ct)
    {
        var (totalFunded, totalNonFunded) = await ComputeAggregatedGroupExposureAsync(group, ct);
        decimal totalExposure = totalFunded + totalNonFunded;
        decimal remainingLimit = group.StatutoryGroupLimitBdt - totalExposure;
        bool isBreached = totalExposure > group.StatutoryGroupLimitBdt;

        var memberDtos = group.MemberDetails.Select(m => new GroupMemberDetailDto(
            m.CifNumber,
            m.EntityName,
            m.Role.ToString(),
            m.ShareholdingPercentage,
            m.RegistrationNumber,
            m.LinkedAtUtc)).ToList();

        var uboDtos = group.BeneficialOwners.Select(b => new BeneficialOwnerDto(
            b.OwnerId,
            b.FullName,
            b.NationalId,
            b.OwnerType.ToString(),
            b.ShareholdingPercentage,
            b.IsPepOrIp,
            b.VerificationStatus,
            b.RegisteredAtUtc)).ToList();

        return new CorporateGroupDetailResponse(
            group.Id,
            group.GroupName,
            group.ParentCifNumber,
            group.StatutoryGroupLimitBdt,
            memberDtos,
            uboDtos,
            totalFunded,
            totalNonFunded,
            totalExposure,
            remainingLimit,
            isBreached);
    }

    // =========================================================================
    // 2. Customer Transaction Profile (TP)
    // =========================================================================

    public async Task<CustomerTransactionProfileResponse> CreateOrUpdateTpAsync(
        CreateTransactionProfileRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var existing = await _tpRepository.GetByCifAsync(request.CifNumber, ct);
        if (existing == null)
        {
            var profile = new CustomerTransactionProfile(
                request.CifNumber,
                Money.Bdt(request.MonthlyEstimatedTurnover),
                Money.Bdt(request.MaxCashDepositAmount),
                request.MaxCashDepositCount,
                Money.Bdt(request.MaxCashWithdrawalAmount),
                request.MaxCashWithdrawalCount,
                Money.Bdt(request.MaxTransferAmount),
                request.MaxTransferCount,
                Money.Bdt(request.MaxAdcAmount),
                request.MaxAdcCount,
                DateTime.UtcNow);

            await _tpRepository.AddAsync(profile, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return MapTpToResponse(profile);
        }
        else
        {
            existing.UpdateDeclaredLimits(
                Money.Bdt(request.MonthlyEstimatedTurnover),
                Money.Bdt(request.MaxCashDepositAmount),
                request.MaxCashDepositCount,
                Money.Bdt(request.MaxCashWithdrawalAmount),
                request.MaxCashWithdrawalCount,
                Money.Bdt(request.MaxTransferAmount),
                request.MaxTransferCount,
                Money.Bdt(request.MaxAdcAmount),
                request.MaxAdcCount,
                "SYSTEM_COMPLIANCE");

            await _tpRepository.UpdateAsync(existing, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return MapTpToResponse(existing);
        }
    }

    public async Task<CustomerTransactionProfileResponse> RecordTpTransactionAsync(
        string cifNumber,
        RecordTpTransactionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var profile = await _tpRepository.GetByCifAsync(cifNumber, ct)
            ?? throw new KeyNotFoundException($"Transaction Profile for CIF '{cifNumber}' was not found.");

        profile.RecordTransaction(
            request.Category,
            Money.Bdt(request.Amount),
            request.TransactionDate ?? DateTime.UtcNow);

        await _tpRepository.UpdateAsync(profile, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapTpToResponse(profile);
    }

    public async Task<CustomerTransactionProfileResponse> GetTpByCifAsync(string cifNumber, CancellationToken ct = default)
    {
        var profile = await _tpRepository.GetByCifAsync(cifNumber, ct)
            ?? throw new KeyNotFoundException($"Transaction Profile for CIF '{cifNumber}' was not found.");

        return MapTpToResponse(profile);
    }

    public async Task<CustomerTransactionProfileResponse> ClearTpBreachAsync(
        string cifNumber,
        ClearTpBreachRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var profile = await _tpRepository.GetByCifAsync(cifNumber, ct)
            ?? throw new KeyNotFoundException($"Transaction Profile for CIF '{cifNumber}' was not found.");

        profile.ClearBreachAfterEdd(request.ReviewedByUserId, request.Justification);

        await _tpRepository.UpdateAsync(profile, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapTpToResponse(profile);
    }

    private static CustomerTransactionProfileResponse MapTpToResponse(CustomerTransactionProfile tp)
    {
        return new CustomerTransactionProfileResponse(
            tp.CifNumber,
            tp.MonthlyEstimatedTurnover.Amount,
            tp.MaxMonthlyCashDepositAmount.Amount,
            tp.MaxMonthlyCashDepositCount,
            tp.MaxMonthlyCashWithdrawalAmount.Amount,
            tp.MaxMonthlyCashWithdrawalCount,
            tp.MaxMonthlyTransferAmount.Amount,
            tp.MaxMonthlyTransferCount,
            tp.MaxMonthlyAdcAmount.Amount,
            tp.MaxMonthlyAdcCount,
            tp.CurrentMonthYear,
            tp.CurrentMonthCashDepositAmount.Amount,
            tp.CurrentMonthCashDepositCount,
            tp.CurrentMonthCashWithdrawalAmount.Amount,
            tp.CurrentMonthCashWithdrawalCount,
            tp.CurrentMonthTransferAmount.Amount,
            tp.CurrentMonthTransferCount,
            tp.CurrentMonthAdcAmount.Amount,
            tp.CurrentMonthAdcCount,
            tp.IsTpBreached,
            tp.MaxVariancePercentage,
            tp.LastBreachReason,
            tp.LastAssessedAtUtc,
            tp.LastReviewedByUserId,
            tp.LastReviewedAtUtc);
    }

    // =========================================================================
    // 3. ADC Central Reconciliation & Rectification
    // =========================================================================

    public async Task<AdcReconciliationBatchResponse> CreateReconciliationBatchAsync(
        CreateReconciliationBatchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = new AdcReconciliationBatch(
            request.BatchId,
            request.Network,
            request.ReconciliationDate,
            request.SettlementGlAccount,
            request.SuspenseGlAccount,
            request.CreatedByUserId,
            DateTime.UtcNow);

        await _adcReconciliationRepository.AddAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBatchToResponse(batch);
    }

    public async Task<AdcReconciliationBatchResponse> IngestSwitchRecordAsync(
        string batchId,
        IngestSwitchRecordRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = await _adcReconciliationRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"ADC reconciliation batch '{batchId}' was not found.");

        Money switchAmount = Money.Bdt(request.SwitchAmount);
        Money? cbsAmount = request.CbsAmount.HasValue ? Money.Bdt(request.CbsAmount.Value) : null;

        batch.IngestSwitchRecord(
            request.Stan,
            request.Rrn,
            request.TerminalId,
            request.AccountNumber,
            switchAmount,
            cbsAmount,
            request.TransactionTime);

        await _adcReconciliationRepository.UpdateAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBatchToResponse(batch);
    }

    public async Task<RectificationVoucherDto> GenerateRectificationVoucherAsync(
        string batchId,
        GenerateRectificationVoucherRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = await _adcReconciliationRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"ADC reconciliation batch '{batchId}' was not found.");

        var voucher = batch.GenerateRectificationVoucher(
            request.EntryId,
            request.Action,
            request.CustomerOrTargetAccount,
            request.AuthorizedByUserId,
            request.Narration);

        await _adcReconciliationRepository.UpdateAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new RectificationVoucherDto(
            voucher.VoucherNumber,
            voucher.EntryId,
            voucher.Stan,
            voucher.Rrn,
            voucher.DebitAccountOrGl,
            voucher.CreditAccountOrGl,
            voucher.VoucherAmount.Amount,
            voucher.Action.ToString(),
            voucher.Narration,
            voucher.AuthorizedByUserId,
            voucher.IssuedAtUtc);
    }

    public async Task<AdcReconciliationBatchResponse> GetReconciliationBatchAsync(string batchId, CancellationToken ct = default)
    {
        var batch = await _adcReconciliationRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"ADC reconciliation batch '{batchId}' was not found.");

        return MapBatchToResponse(batch);
    }

    public async Task<AdcReconciliationBatchResponse> CloseReconciliationBatchAsync(
        string batchId,
        string closedByUserId,
        CancellationToken ct = default)
    {
        var batch = await _adcReconciliationRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"ADC reconciliation batch '{batchId}' was not found.");

        batch.CloseBatch(closedByUserId);

        await _adcReconciliationRepository.UpdateAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBatchToResponse(batch);
    }

    private static AdcReconciliationBatchResponse MapBatchToResponse(AdcReconciliationBatch batch)
    {
        var entries = batch.Entries.Select(e => new AdcReconciliationEntryDto(
            e.EntryId,
            e.Stan,
            e.Rrn,
            e.TerminalId,
            e.AccountNumber,
            e.SwitchAmount.Amount,
            e.CbsAmount?.Amount,
            e.DifferenceAmount.Amount,
            e.MatchStatus.ToString(),
            e.ActionTaken?.ToString(),
            e.RectificationVoucherNumber,
            e.IsRectified,
            e.TransactionTimestampUtc)).ToList();

        var vouchers = batch.Vouchers.Select(v => new RectificationVoucherDto(
            v.VoucherNumber,
            v.EntryId,
            v.Stan,
            v.Rrn,
            v.DebitAccountOrGl,
            v.CreditAccountOrGl,
            v.VoucherAmount.Amount,
            v.Action.ToString(),
            v.Narration,
            v.AuthorizedByUserId,
            v.IssuedAtUtc)).ToList();

        return new AdcReconciliationBatchResponse(
            batch.BatchId,
            batch.Network.ToString(),
            batch.ReconciliationDate,
            batch.SettlementGlAccount,
            batch.SuspenseGlAccount,
            batch.TotalRecords,
            batch.TotalMatched,
            batch.TotalMismatched,
            batch.TotalDiscrepancyAmount.Amount,
            batch.Status.ToString(),
            batch.CreatedByUserId,
            batch.CreatedAtUtc,
            batch.ClosedAtUtc,
            entries,
            vouchers);
    }
}
