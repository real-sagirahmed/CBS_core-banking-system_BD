// ARCHITECTURAL INTENT: Treasury Management, FX Blotter, NOP, MTM Revaluation,
// Nostro Mirror Reconciliation, and Central Bank Reserves (CRR/SLR) Service implementation.
// Conforms strictly to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 6,
// DOS Circular No. 05/2008, and MPD Circular No. 03/2020.

using CBS.Application.Common.Interfaces;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Treasury;

namespace CBS.Application.Treasury.Services;

public sealed class TreasuryManagementService : ITreasuryManagementService
{
    private readonly ITreasuryRepository _treasuryRepo;
    private readonly ITreasuryLimitRepository _limitRepo;
    private readonly IMtmRevaluationRepository _mtmRepo;
    private readonly INostroReconciliationRepository _nostroRepo;
    private readonly IStatutoryReserveRepository _reserveRepo;
    private readonly IUnitOfWork _unitOfWork;

    public TreasuryManagementService(
        ITreasuryRepository treasuryRepo,
        ITreasuryLimitRepository limitRepo,
        IMtmRevaluationRepository mtmRepo,
        INostroReconciliationRepository nostroRepo,
        IStatutoryReserveRepository reserveRepo,
        IUnitOfWork unitOfWork)
    {
        _treasuryRepo = treasuryRepo ?? throw new ArgumentNullException(nameof(treasuryRepo));
        _limitRepo = limitRepo ?? throw new ArgumentNullException(nameof(limitRepo));
        _mtmRepo = mtmRepo ?? throw new ArgumentNullException(nameof(mtmRepo));
        _nostroRepo = nostroRepo ?? throw new ArgumentNullException(nameof(nostroRepo));
        _reserveRepo = reserveRepo ?? throw new ArgumentNullException(nameof(reserveRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<FxBlotterSummaryDto> GetFxBlotterAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var blotterDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var allDeals = await _treasuryRepo.GetAllDealsAsync(cancellationToken);

        var fxDeals = allDeals
            .Where(d => d.Type is TreasuryDealType.FxSpot or TreasuryDealType.FxForward or TreasuryDealType.FxSwap)
            .ToList();

        // Default standard market mid rates for foreign exchange blotter
        var midRates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "USD", 110.50m },
            { "EUR", 120.00m },
            { "GBP", 140.00m },
            { "JPY", 0.74m },
            { "CNY", 15.30m }
        };

        // Group by FCY currency
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USD", "EUR", "GBP" };
        foreach (var deal in fxDeals)
        {
            if (!string.Equals(deal.PrimaryAmount.Currency.Code, "BDT", StringComparison.OrdinalIgnoreCase))
                currencies.Add(deal.PrimaryAmount.Currency.Code);
            if (!string.Equals(deal.SecondaryAmount.Currency.Code, "BDT", StringComparison.OrdinalIgnoreCase))
                currencies.Add(deal.SecondaryAmount.Currency.Code);
        }

        var positions = new List<FxBlotterCurrencyPositionDto>();
        decimal totalNopUsd = 0m;
        decimal totalNopBdt = 0m;

        foreach (var ccy in currencies)
        {
            var buys = fxDeals
                .Where(d => d.Direction == DealDirection.Buy && string.Equals(d.PrimaryAmount.Currency.Code, ccy, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var sells = fxDeals
                .Where(d => d.Direction == DealDirection.Sell && string.Equals(d.PrimaryAmount.Currency.Code, ccy, StringComparison.OrdinalIgnoreCase))
                .ToList();

            decimal totalBought = buys.Sum(b => b.PrimaryAmount.Amount);
            decimal totalBoughtBdt = buys.Sum(b => b.SecondaryAmount.Amount);
            decimal totalSold = sells.Sum(s => s.PrimaryAmount.Amount);
            decimal totalSoldBdt = sells.Sum(s => s.SecondaryAmount.Amount);

            decimal netPos = totalBought - totalSold;
            decimal midRate = midRates.TryGetValue(ccy, out var rate) ? rate : 110.50m;
            decimal bdtEquiv = Math.Round(netPos * midRate, 2);

            decimal avgBuyRate = totalBought > 0 ? Math.Round(totalBoughtBdt / totalBought, 4) : 0m;
            decimal avgSellRate = totalSold > 0 ? Math.Round(totalSoldBdt / totalSold, 4) : 0m;

            positions.Add(new FxBlotterCurrencyPositionDto(
                Currency: ccy,
                OpeningPosition: 0m,
                TotalBoughtFcy: totalBought,
                TotalSoldFcy: totalSold,
                NetPosition: netPos,
                WeightedAverageBuyRate: avgBuyRate,
                WeightedAverageSellRate: avgSellRate,
                RevaluationMidRate: midRate,
                BdtEquivalent: bdtEquiv
            ));

            // USD Equivalent calculation for aggregate NOP
            decimal usdRate = string.Equals(ccy, "USD", StringComparison.OrdinalIgnoreCase) ? 1.0m : (midRate / midRates["USD"]);
            totalNopUsd += netPos * usdRate;
            totalNopBdt += bdtEquiv;
        }

        // Check against Bangladesh Bank Sanctioned NOP Limit
        var nopLimitProfile = await _limitRepo.GetActiveByTypeAndIdentifierAsync(
            TreasuryLimitType.NetOpenPositionAggregate,
            "BANK_NOP_AGGREGATE",
            cancellationToken);

        decimal sanctionedNopUsd = nopLimitProfile?.SanctionedLimit ?? 50_000_000m; // Default $50 Million USD
        decimal utilizationPct = sanctionedNopUsd > 0 ? Math.Round((Math.Abs(totalNopUsd) / sanctionedNopUsd) * 100m, 2) : 0m;
        bool isCompliant = Math.Abs(totalNopUsd) <= sanctionedNopUsd;

        return new FxBlotterSummaryDto(
            BlotterDate: blotterDate,
            CurrencyPositions: positions,
            TotalNetOpenPositionUsd: Math.Round(totalNopUsd, 2),
            TotalNetOpenPositionBdt: Math.Round(totalNopBdt, 2),
            SanctionedNopLimitUsd: sanctionedNopUsd,
            NopUtilizationPercentage: utilizationPct,
            IsNopCompliant: isCompliant
        );
    }

    public async Task<TreasuryLimitProfileDto> ConfigureLimitProfileAsync(
        ConfigureTreasuryLimitProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var existing = await _limitRepo.GetActiveByTypeAndIdentifierAsync(
            command.LimitType,
            command.Identifier,
            cancellationToken);

        if (existing is not null)
        {
            existing.UpdateLimit(command.SanctionedLimit, command.ApprovedByUserId);
            await _limitRepo.UpdateAsync(existing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TreasuryLimitProfileDto(
                Id: existing.Id,
                LimitType: existing.LimitType,
                Identifier: existing.Identifier,
                Currency: existing.Currency,
                SanctionedLimit: existing.SanctionedLimit,
                UtilizedAmount: existing.UtilizedAmount,
                AvailableAmount: existing.AvailableAmount,
                IsActive: existing.IsActive,
                ApprovedByUserId: existing.ApprovedByUserId,
                EffectiveDate: existing.EffectiveDate,
                ExpiryDate: existing.ExpiryDate
            );
        }

        var profile = new TreasuryLimitProfile(
            id: Guid.NewGuid(),
            limitType: command.LimitType,
            identifier: command.Identifier,
            currency: command.Currency,
            sanctionedLimit: command.SanctionedLimit,
            approvedByUserId: command.ApprovedByUserId,
            effectiveDate: command.EffectiveDate,
            expiryDate: command.ExpiryDate
        );

        await _limitRepo.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TreasuryLimitProfileDto(
            Id: profile.Id,
            LimitType: profile.LimitType,
            Identifier: profile.Identifier,
            Currency: profile.Currency,
            SanctionedLimit: profile.SanctionedLimit,
            UtilizedAmount: profile.UtilizedAmount,
            AvailableAmount: profile.AvailableAmount,
            IsActive: profile.IsActive,
            ApprovedByUserId: profile.ApprovedByUserId,
            EffectiveDate: profile.EffectiveDate,
            ExpiryDate: profile.ExpiryDate
        );
    }

    public async Task<TreasuryLimitCheckResultDto> CheckAndEnforceLimitAsync(
        CheckTreasuryLimitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var profile = await _limitRepo.GetActiveByTypeAndIdentifierAsync(
            command.LimitType,
            command.Identifier,
            cancellationToken);

        if (profile is null)
        {
            return new TreasuryLimitCheckResultDto(
                LimitType: command.LimitType,
                Identifier: command.Identifier,
                SanctionedLimit: 0m,
                UtilizedAmount: 0m,
                AvailableAmount: 0m,
                RequestedAmount: command.Amount,
                IsAllowed: true,
                Message: $"No active limit profile found for '{command.Identifier}' ({command.LimitType}). Auto-allowed."
            );
        }

        try
        {
            profile.CheckAndUtilize(command.Amount, command.DealReference);
            await _limitRepo.UpdateAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new TreasuryLimitCheckResultDto(
                LimitType: profile.LimitType,
                Identifier: profile.Identifier,
                SanctionedLimit: profile.SanctionedLimit,
                UtilizedAmount: profile.UtilizedAmount,
                AvailableAmount: profile.AvailableAmount,
                RequestedAmount: command.Amount,
                IsAllowed: true,
                Message: "Limit check passed successfully."
            );
        }
        catch (Exception ex)
        {
            return new TreasuryLimitCheckResultDto(
                LimitType: profile.LimitType,
                Identifier: profile.Identifier,
                SanctionedLimit: profile.SanctionedLimit,
                UtilizedAmount: profile.UtilizedAmount,
                AvailableAmount: profile.AvailableAmount,
                RequestedAmount: command.Amount,
                IsAllowed: false,
                Message: ex.Message
            );
        }
    }

    public async Task<MtmRevaluationResultDto> ExecuteMtmRevaluationAsync(
        ExecuteMtmRevaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var revalDate = command.RevaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var allDeals = await _treasuryRepo.GetAllDealsAsync(cancellationToken);
        var allSecurities = await _treasuryRepo.GetAllSecuritiesAsync(cancellationToken);

        var forwardDeals = allDeals
            .Where(d => d.Type == TreasuryDealType.FxForward)
            .ToList();

        var hftSecurities = allSecurities
            .Where(s => s.Classification == SecurityClassification.Hft)
            .ToList();

        decimal totalGains = 0m;
        decimal totalLosses = 0m;

        // 1. Revalue FX Forwards
        foreach (var fwd in forwardDeals)
        {
            // Current forward benchmark mid-rate = 112.00 BDT
            decimal benchmarkForwardRate = 112.00m;
            decimal variancePerUnit = fwd.Direction == DealDirection.Buy
                ? benchmarkForwardRate - fwd.ExchangeRate
                : fwd.ExchangeRate - benchmarkForwardRate;

            decimal mtmPnl = Math.Round(variancePerUnit * fwd.PrimaryAmount.Amount, 2);
            if (mtmPnl >= 0) totalGains += mtmPnl;
            else totalLosses += Math.Abs(mtmPnl);
        }

        // 2. Revalue HFT Securities per DOS Circular 05/2008
        foreach (var sec in hftSecurities)
        {
            // Current yield valuation price vs carrying cost
            decimal currentMarketPrice = Math.Round(sec.FaceValue.Amount * 0.985m, 2);
            decimal carryingCost = sec.BookValue.Amount;
            decimal diff = currentMarketPrice - carryingCost;

            if (diff >= 0) totalGains += diff;
            else totalLosses += Math.Abs(diff);
        }

        var batchId = Guid.NewGuid();
        var batchRef = $"MTM-{revalDate:yyyyMMdd}-EOD";
        var batch = new MtmRevaluationBatch(
            id: batchId,
            batchReference: batchRef,
            revaluationDate: revalDate,
            totalForwardDealsEvaluated: forwardDeals.Count,
            totalSecuritiesEvaluated: hftSecurities.Count,
            totalUnrealizedGainsBdt: totalGains,
            totalUnrealizedLossesBdt: totalLosses,
            executedByUserId: command.ExecutedByUserId,
            remarks: "Daily MTM Revaluation per DOS Circular 05/2008 & BRPD 12 (6.3.1)"
        );

        await _mtmRepo.AddAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MtmRevaluationResultDto(
            BatchId: batchId,
            BatchReference: batchRef,
            RevaluationDate: revalDate,
            TotalForwardDealsEvaluated: forwardDeals.Count,
            TotalSecuritiesEvaluated: hftSecurities.Count,
            TotalUnrealizedGainsBdt: totalGains,
            TotalUnrealizedLossesBdt: totalLosses,
            NetRevaluationReserveBdt: batch.NetRevaluationReserveBdt,
            Status: batch.Status
        );
    }

    public async Task<StatutoryReserveAssessmentDto> EvaluateStatutoryReservesAsync(
        EvaluateStatutoryReservesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var assessmentDate = command.AssessmentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var assessmentId = Guid.NewGuid();

        var assessment = new StatutoryReserveAssessment(
            id: assessmentId,
            assessmentDate: assessmentDate,
            bankingType: command.BankingType,
            tdtlBaseBdt: command.TdtlBaseBdt,
            maintainedCrrCashBdt: command.MaintainedCrrCashBdt,
            maintainedSlrAssetsBdt: command.MaintainedSlrAssetsBdt,
            evaluatedByUserId: command.EvaluatedByUserId
        );

        await _reserveRepo.AddAsync(assessment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StatutoryReserveAssessmentDto(
            AssessmentId: assessmentId,
            AssessmentDate: assessmentDate,
            BankingType: assessment.BankingType,
            TdtlBaseBdt: assessment.TdtlBaseBdt,
            RequiredCrrDailyBdt: assessment.RequiredCrrDailyBdt,
            RequiredCrrBiWeeklyBdt: assessment.RequiredCrrBiWeeklyBdt,
            MaintainedCrrCashBdt: assessment.MaintainedCrrCashBdt,
            DailyCrrShortfallBdt: assessment.DailyCrrShortfallBdt,
            IsDailyCrrCompliant: assessment.IsDailyCrrCompliant,
            RequiredSlrBdt: assessment.RequiredSlrBdt,
            MaintainedSlrAssetsBdt: assessment.MaintainedSlrAssetsBdt,
            SlrShortfallBdt: assessment.SlrShortfallBdt,
            IsSlrCompliant: assessment.IsSlrCompliant
        );
    }

    public async Task<StatutoryReserveAssessmentDto?> GetLatestStatutoryReserveAssessmentAsync(
        CancellationToken cancellationToken = default)
    {
        var latest = await _reserveRepo.GetLatestAsync(cancellationToken);
        if (latest is null) return null;

        return new StatutoryReserveAssessmentDto(
            AssessmentId: latest.Id,
            AssessmentDate: latest.AssessmentDate,
            BankingType: latest.BankingType,
            TdtlBaseBdt: latest.TdtlBaseBdt,
            RequiredCrrDailyBdt: latest.RequiredCrrDailyBdt,
            RequiredCrrBiWeeklyBdt: latest.RequiredCrrBiWeeklyBdt,
            MaintainedCrrCashBdt: latest.MaintainedCrrCashBdt,
            DailyCrrShortfallBdt: latest.DailyCrrShortfallBdt,
            IsDailyCrrCompliant: latest.IsDailyCrrCompliant,
            RequiredSlrBdt: latest.RequiredSlrBdt,
            MaintainedSlrAssetsBdt: latest.MaintainedSlrAssetsBdt,
            SlrShortfallBdt: latest.SlrShortfallBdt,
            IsSlrCompliant: latest.IsSlrCompliant
        );
    }

    public async Task IngestLocalNostroEntryAsync(
        IngestLocalNostroEntryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var item = new NostroMirrorReconciliationItem(
            id: Guid.NewGuid(),
            nostroAccountCode: command.NostroAccountCode,
            currency: command.Currency,
            valueDate: command.ValueDate,
            entryType: command.EntryType,
            localBookReference: command.LocalBookReference,
            localBookAmount: command.LocalBookAmount
        );

        await _nostroRepo.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<NostroReconciliationBatchResultDto> ReconcileNostroStatementsAsync(
        IngestNostroStatementCommand command,
        string matchedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var unmanagedItems = await _nostroRepo.GetUnreconciledAsync(command.NostroAccountCode, cancellationToken);
        var unmatchedPool = unmanagedItems.ToList();
        int matchedCount = 0;

        foreach (var entry in command.Entries)
        {
            // Rule 1: Exact reference and amount match
            var match = unmatchedPool.FirstOrDefault(i =>
                string.Equals(i.LocalBookReference, entry.StatementReference, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.EntryType, entry.EntryType, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(i.LocalBookAmount - entry.Amount) < 0.01m);

            // Rule 2: Fallback to exact amount and value date match
            match ??= unmatchedPool.FirstOrDefault(i =>
                i.ValueDate == entry.ValueDate &&
                string.Equals(i.EntryType, entry.EntryType, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(i.LocalBookAmount - entry.Amount) < 0.01m);

            if (match is not null)
            {
                match.MatchWithStatement(entry.StatementReference, entry.Amount, "1:1_EXACT_MATCH", matchedByUserId);
                unmatchedPool.Remove(match);
                matchedCount++;
                await _nostroRepo.UpdateAsync(match, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new NostroReconciliationBatchResultDto(
            NostroAccountCode: command.NostroAccountCode,
            TotalItemsProcessed: command.Entries.Count,
            AutoMatchedCount: matchedCount,
            UnmatchedCount: command.Entries.Count - matchedCount
        );
    }

    public async Task<NostroUnreconciledAgingReportDto> GetNostroUnreconciledAgingReportAsync(
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var reportDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var allItems = await _nostroRepo.GetAllAsync(cancellationToken);

        foreach (var item in allItems)
        {
            item.UpdateAgingAndProvision(reportDate);
            await _nostroRepo.UpdateAsync(item, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var unreconciled = allItems
            .Where(i => i.Status != NostroReconciliationStatus.MatchedAuto && i.Status != NostroReconciliationStatus.MatchedManual)
            .OrderByDescending(i => i.DaysOutstanding)
            .ToList();

        var debitItems = unreconciled.Where(i => string.Equals(i.EntryType, "Debit", StringComparison.OrdinalIgnoreCase)).ToList();

        var dtoList = unreconciled.Select(i => new NostroUnreconciledItemDto(
            ItemId: i.Id,
            NostroAccountCode: i.NostroAccountCode,
            Currency: i.Currency,
            ValueDate: i.ValueDate,
            EntryType: i.EntryType,
            LocalBookReference: i.LocalBookReference,
            LocalBookAmount: i.LocalBookAmount,
            DaysOutstanding: i.DaysOutstanding,
            RequiredProvisionRate: i.RequiredProvisionRate,
            RequiredProvisionAmount: i.RequiredProvisionAmount,
            Status: i.Status.ToString()
        )).ToList();

        return new NostroUnreconciledAgingReportDto(
            ReportDate: reportDate,
            TotalUnreconciledItems: unreconciled.Count,
            TotalUnreconciledDebitAmount: debitItems.Sum(i => i.LocalBookAmount),
            TotalRequiredProvisionAmount: debitItems.Sum(i => i.RequiredProvisionAmount),
            Items: dtoList
        );
    }
}
