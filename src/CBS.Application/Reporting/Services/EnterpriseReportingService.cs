using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Common.Interfaces;
using CBS.Application.Reporting.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Remittance;
using CBS.Domain.Reporting;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Reporting.Services;

public sealed class EnterpriseReportingService : IEnterpriseReportingService
{
    private readonly IRemittanceIncentiveRepository _remittanceRepository;
    private readonly IDailyCleanCashRepository _cleanCashRepository;
    private readonly ILoanAccountRepository _loanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EnterpriseReportingService(
        IRemittanceIncentiveRepository remittanceRepository,
        IDailyCleanCashRepository cleanCashRepository,
        ILoanAccountRepository loanRepository,
        IUnitOfWork unitOfWork)
    {
        _remittanceRepository = remittanceRepository;
        _cleanCashRepository = cleanCashRepository;
        _loanRepository = loanRepository;
        _unitOfWork = unitOfWork;
    }

    // =========================================================================
    // 1. Wage Earners' Remittance 2.5% Government Cash Incentive
    // =========================================================================

    public async Task<RemittanceIncentiveResponseDto> DisburseRemittanceIncentiveAsync(
        DisburseRemittanceIncentiveRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        string incentiveId = $"INC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var record = new RemittanceCashIncentiveRecord(
            incentiveId: incentiveId,
            remittanceReferenceNumber: request.RemittanceReferenceNumber,
            exchangeHouseCode: request.ExchangeHouseCode,
            remitterName: request.RemitterName,
            remitterCountry: request.RemitterCountry,
            beneficiaryAccountNumber: request.BeneficiaryAccountNumber,
            beneficiaryCif: request.BeneficiaryCif,
            remittanceAmountBdt: Money.Bdt(request.RemittanceAmountBdt),
            disbursingBranchCode: request.DisbursingBranchCode,
            disbursedByUserId: request.DisbursedByUserId,
            disbursedAtUtc: DateTime.UtcNow);

        await _remittanceRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToIncentiveDto(record);
    }

    public async Task<IReadOnlyList<RemittanceIncentiveResponseDto>> GetPendingIncentiveClaimsAsync(CancellationToken ct = default)
    {
        var records = await _remittanceRepository.GetPendingClaimsAsync(ct);
        return records.Select(MapToIncentiveDto).ToList();
    }

    public async Task<IReadOnlyList<RemittanceIncentiveResponseDto>> SubmitIncentiveClaimsAsync(
        SubmitRemittanceIncentiveBatchClaimRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.IncentiveIds == null || request.IncentiveIds.Count == 0)
            throw new ArgumentException("At least one incentive ID is required for claim submission.", nameof(request));

        var updatedList = new List<RemittanceIncentiveResponseDto>();

        foreach (var id in request.IncentiveIds)
        {
            var record = await _remittanceRepository.GetByIdAsync(id, ct);
            if (record != null)
            {
                record.SubmitClaimToBangladeshBank(request.ClaimBatchReference);
                await _remittanceRepository.UpdateAsync(record, ct);
                updatedList.Add(MapToIncentiveDto(record));
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return updatedList;
    }

    public async Task<RemittanceIncentiveResponseDto> RecordReimbursementAsync(
        string incentiveId,
        RecordCentralBankReimbursementRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var record = await _remittanceRepository.GetByIdAsync(incentiveId, ct)
            ?? throw new KeyNotFoundException($"Incentive record '{incentiveId}' was not found.");

        record.RecordCentralBankReimbursement(request.CentralBankVoucherReference);
        await _remittanceRepository.UpdateAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToIncentiveDto(record);
    }

    // =========================================================================
    // 2. Branch Daily Clean Cash & Proofing
    // =========================================================================

    public async Task<DailyCleanCashResponseDto> GenerateDailyCleanCashAsync(
        GenerateDailyCleanCashRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        string sheetId = $"CC-{request.BranchCode.Trim()}-{request.OperatingDate:yyyyMMdd}";
        var existing = await _cleanCashRepository.GetByIdAsync(sheetId, ct);
        if (existing != null)
        {
            return MapToCleanCashDto(existing);
        }

        var sheet = new DailyCleanCashSheet(
            sheetId: sheetId,
            branchCode: request.BranchCode,
            operatingDate: request.OperatingDate,
            openingVaultCash: Money.Bdt(request.OpeningVaultCash),
            openingTellerCash: Money.Bdt(request.OpeningTellerCash),
            totalCashReceived: Money.Bdt(request.TotalCashReceived),
            totalCashPaid: Money.Bdt(request.TotalCashPaid),
            physicalClosingCash: Money.Bdt(request.PhysicalClosingCash),
            createdAtUtc: DateTime.UtcNow);

        await _cleanCashRepository.AddAsync(sheet, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToCleanCashDto(sheet);
    }

    public async Task<DailyCleanCashResponseDto> CertifyDailyCleanCashAsync(
        string sheetId,
        CertifyDailyCleanCashRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var sheet = await _cleanCashRepository.GetByIdAsync(sheetId, ct)
            ?? throw new KeyNotFoundException($"Clean cash sheet '{sheetId}' was not found.");

        sheet.CertifyAndProofCleanCash(request.HeadCashierUserId, request.ManagerUserId);
        await _cleanCashRepository.UpdateAsync(sheet, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToCleanCashDto(sheet);
    }

    public async Task<DailyCleanCashResponseDto> GetDailyCleanCashAsync(string sheetId, CancellationToken ct = default)
    {
        var sheet = await _cleanCashRepository.GetByIdAsync(sheetId, ct)
            ?? throw new KeyNotFoundException($"Clean cash sheet '{sheetId}' was not found.");

        return MapToCleanCashDto(sheet);
    }

    // =========================================================================
    // 3. Statutory Regulatory Returns (Large Loans & NPL Portfolio)
    // =========================================================================

    public async Task<LargeLoanPortfolioResponseDto> GenerateLargeLoanExposureReportAsync(
        decimal capitalBaseBdt,
        CancellationToken ct = default)
    {
        if (capitalBaseBdt <= 0)
            throw new ArgumentException("Bank capital base must be greater than zero.", nameof(capitalBaseBdt));

        decimal largeLoanThreshold = capitalBaseBdt * 0.10m; // 10% of capital base (BRPD Circular 12/2024 & 02/2022)

        var loans = await _loanRepository.GetAllLoansAsync(ct);
        var borrowerGroups = loans
            .GroupBy(l => l.Cif.Value)
            .Select(g => new
            {
                CifNumber = g.Key,
                TotalFunded = g.Sum(l => l.OutstandingPrincipal.Amount),
                TotalNonFunded = 0m, // Additional non-funded facilities if tracked
                TotalExposure = g.Sum(l => l.OutstandingPrincipal.Amount)
            })
            .Where(b => b.TotalExposure >= largeLoanThreshold)
            .OrderByDescending(b => b.TotalExposure)
            .ToList();

        var items = borrowerGroups.Select(b => new LargeLoanBorrowerItemDto(
            CifNumber: b.CifNumber,
            BorrowerName: $"Borrower {b.CifNumber}",
            FundedExposureBdt: b.TotalFunded,
            NonFundedExposureBdt: b.TotalNonFunded,
            TotalExposureBdt: b.TotalExposure,
            PercentageOfCapitalBase: Math.Round((b.TotalExposure / capitalBaseBdt) * 100m, 2)
        )).ToList();

        return new LargeLoanPortfolioResponseDto(
            CapitalBaseBdt: capitalBaseBdt,
            LargeLoanThresholdBdt: largeLoanThreshold,
            TotalLargeLoansCount: items.Count,
            TotalLargeLoanExposureBdt: items.Sum(i => i.TotalExposureBdt),
            Items: items);
    }

    public async Task<NplClassificationSummaryDto> GenerateNplClassificationSummaryAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var loans = (await _loanRepository.GetAllLoansAsync(ct)).ToList();

        decimal standardAmt = loans.Where(l => l.Classification == LoanClassificationGrade.Standard).Sum(l => l.OutstandingPrincipal.Amount);
        decimal smaAmt = loans.Where(l => l.Classification == LoanClassificationGrade.SpecialMentionAccount_SMA).Sum(l => l.OutstandingPrincipal.Amount);
        decimal ssAmt = loans.Where(l => l.Classification == LoanClassificationGrade.SubStandard_SS).Sum(l => l.OutstandingPrincipal.Amount);
        decimal dfAmt = loans.Where(l => l.Classification == LoanClassificationGrade.Doubtful_DF).Sum(l => l.OutstandingPrincipal.Amount);
        decimal blAmt = loans.Where(l => l.Classification == LoanClassificationGrade.BadLoss_BL).Sum(l => l.OutstandingPrincipal.Amount);

        decimal totalOutstanding = standardAmt + smaAmt + ssAmt + dfAmt + blAmt;
        decimal totalNpl = ssAmt + dfAmt + blAmt;
        decimal nplRatio = totalOutstanding > 0 ? Math.Round((totalNpl / totalOutstanding) * 100m, 2) : 0m;
        decimal totalProvision = loans.Sum(l => l.RequiredGeneralProvision.Amount + l.RequiredSpecificProvision.Amount);

        return new NplClassificationSummaryDto(
            AsOfDate: asOfDate,
            TotalLoansCount: loans.Count,
            TotalOutstandingBdt: totalOutstanding,
            StandardAmountBdt: standardAmt,
            SmaAmountBdt: smaAmt,
            SubStandardAmountBdt: ssAmt,
            DoubtfulAmountBdt: dfAmt,
            BadLossAmountBdt: blAmt,
            TotalNplAmountBdt: totalNpl,
            NplRatioPercentage: nplRatio,
            TotalRequiredProvisionBdt: totalProvision);
    }

    // =========================================================================
    // DTO Mappings
    // =========================================================================

    private static RemittanceIncentiveResponseDto MapToIncentiveDto(RemittanceCashIncentiveRecord r) =>
        new(r.IncentiveId,
            r.RemittanceReferenceNumber,
            r.ExchangeHouseCode,
            r.RemitterName,
            r.RemitterCountry,
            r.BeneficiaryAccountNumber,
            r.BeneficiaryCif,
            r.RemittanceAmountBdt.Amount,
            r.IncentiveRate,
            r.IncentiveAmountBdt.Amount,
            r.TotalDisbursedBdt.Amount,
            r.DisbursingBranchCode,
            r.DisbursedAtUtc,
            r.DisbursedByUserId,
            r.ClaimStatus.ToString(),
            r.ClaimBatchReference,
            r.ClaimSubmittedAtUtc,
            r.ReimbursedAtUtc,
            r.CentralBankVoucherReference);

    private static DailyCleanCashResponseDto MapToCleanCashDto(DailyCleanCashSheet s) =>
        new(s.SheetId,
            s.BranchCode,
            s.OperatingDate,
            s.OpeningVaultCash.Amount,
            s.OpeningTellerCash.Amount,
            s.TotalCashReceived.Amount,
            s.TotalCashPaid.Amount,
            s.CalculatedClosingCash.Amount,
            s.PhysicalClosingCash.Amount,
            s.CashDifference.Amount,
            s.ProofStatus.ToString(),
            s.DiscrepancyNotes,
            s.CertifiedByHeadCashierUserId,
            s.ApprovedByManagerUserId,
            s.CertifiedAtUtc,
            s.CreatedAtUtc);
}
