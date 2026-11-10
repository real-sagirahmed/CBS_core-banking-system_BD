using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.Reconciliation.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Reconciliation.Commands;

public sealed record RunReconciliationCommand(RunReconciliationRequest Request);
public sealed record FileDisputeCommand(FileDisputeClaimRequest Request);
public sealed record ResolveDisputeCommand(string ClaimId, ResolveDisputeRequest Request);

public class ReconciliationCommandHandler
{
    private readonly IReconciliationRepository _reconRepository;
    private readonly IAdcRepository _adcRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public ReconciliationCommandHandler(
        IReconciliationRepository reconRepository,
        IAdcRepository adcRepository,
        IAccountRepository accountRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _reconRepository = reconRepository ?? throw new ArgumentNullException(nameof(reconRepository));
        _adcRepository = adcRepository ?? throw new ArgumentNullException(nameof(adcRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SettlementReconciliationResponse> HandleRunReconciliationAsync(RunReconciliationCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var coreTxs = await _adcRepository.GetAllAsync(limit: 500, ct);
        var filteredCore = coreTxs
            .Where(t => t.ChannelType == req.Channel)
            .ToList();

        var unmatchedList = new List<UnmatchedReconItem>();
        int matchedCount = 0;

        var switchDict = req.SwitchTransactions.ToDictionary(s => $"{s.Stan}_{s.Rrn}", s => s);
        var networkDict = req.NetworkTransactions.ToDictionary(n => $"{n.Stan}_{n.Rrn}", n => n);

        // 1. Cross-Match Core against Switch and Network
        foreach (var core in filteredCore)
        {
            var key = $"{core.Stan}_{core.Rrn}";
            bool inSwitch = switchDict.TryGetValue(key, out var switchItem);
            bool inNetwork = networkDict.TryGetValue(key, out var networkItem);

            if (inSwitch && inNetwork)
            {
                if (switchItem!.Amount == core.Amount.Amount && networkItem!.Amount == core.Amount.Amount)
                {
                    matchedCount++;
                    core.MarkReconciled();
                }
                else
                {
                    unmatchedList.Add(new UnmatchedReconItem(
                        ItemId: $"UNM-{Guid.NewGuid():N}"[..12],
                        Stan: core.Stan,
                        Rrn: core.Rrn,
                        AccountNumber: core.AccountNumber.Value,
                        Amount: core.Amount.Amount,
                        DiscrepancyType: "AmountMismatch",
                        AgingBucket: ReconAgingBucket.Days1To7,
                        DaysUnreconciled: 1,
                        IsRectified: false));
                }
            }
            else if (!inSwitch && inNetwork)
            {
                unmatchedList.Add(new UnmatchedReconItem(
                    ItemId: $"UNM-{Guid.NewGuid():N}"[..12],
                    Stan: core.Stan,
                    Rrn: core.Rrn,
                    AccountNumber: core.AccountNumber.Value,
                    Amount: core.Amount.Amount,
                    DiscrepancyType: "MissingInSwitch",
                    AgingBucket: ReconAgingBucket.Days1To7,
                    DaysUnreconciled: 1,
                    IsRectified: false));
            }
            else if (inSwitch && !inNetwork)
            {
                unmatchedList.Add(new UnmatchedReconItem(
                    ItemId: $"UNM-{Guid.NewGuid():N}"[..12],
                    Stan: core.Stan,
                    Rrn: core.Rrn,
                    AccountNumber: core.AccountNumber.Value,
                    Amount: core.Amount.Amount,
                    DiscrepancyType: "MissingInNetwork",
                    AgingBucket: ReconAgingBucket.Days1To7,
                    DaysUnreconciled: 1,
                    IsRectified: false));
            }
        }

        // 2. Cross-Match Items present in Switch but missing in Core
        foreach (var s in req.SwitchTransactions)
        {
            var key = $"{s.Stan}_{s.Rrn}";
            bool inCore = filteredCore.Any(c => c.Stan == s.Stan && c.Rrn == s.Rrn);
            if (!inCore)
            {
                unmatchedList.Add(new UnmatchedReconItem(
                    ItemId: $"UNM-{Guid.NewGuid():N}"[..12],
                    Stan: s.Stan,
                    Rrn: s.Rrn,
                    AccountNumber: s.AccountNumber,
                    Amount: s.Amount,
                    DiscrepancyType: "MissingInCore",
                    AgingBucket: ReconAgingBucket.Days1To7,
                    DaysUnreconciled: 1,
                    IsRectified: false));
            }
        }

        var batchId = $"RECON-{req.ReconciliationDate:yyyyMMdd}-{req.Channel}";
        var batch = SettlementReconciliationBatch.Create(
            batchId: batchId,
            reconciliationDate: req.ReconciliationDate,
            channel: req.Channel,
            totalCore: filteredCore.Count,
            totalSwitch: req.SwitchTransactions.Count,
            totalNetwork: req.NetworkTransactions.Count,
            matchedCount: matchedCount,
            unmatchedItems: unmatchedList);

        await _reconRepository.AddBatchAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new SettlementReconciliationResponse(
            BatchId: batch.BatchId,
            ReconciliationDate: batch.ReconciliationDate,
            Channel: batch.Channel.ToString(),
            TotalCore: batch.TotalCoreTransactions,
            TotalSwitch: batch.TotalSwitchTransactions,
            TotalNetwork: batch.TotalNetworkTransactions,
            MatchedCount: batch.MatchedCount,
            MismatchedCount: batch.MismatchedCount,
            Status: batch.Status.ToString(),
            UnmatchedItems: batch.UnmatchedItems.Select(u => new UnmatchedReconItemDto(
                u.ItemId,
                u.Stan,
                u.Rrn,
                u.AccountNumber,
                u.Amount,
                u.DiscrepancyType,
                u.AgingBucket.ToString(),
                u.DaysUnreconciled,
                u.IsRectified)).ToList());
    }

    public async Task<DisputeClaimResponse> HandleFileDisputeAsync(FileDisputeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var accNo = AccountNumber.From(req.AccountNumber);
        var amount = Money.Bdt(req.DisputedAmount);

        var claim = DisputeClaim.LogClaim(
            claimId: req.ClaimId,
            originalTransactionId: req.OriginalTransactionId,
            originalStan: req.OriginalStan,
            originalRrn: req.OriginalRrn,
            accountNumber: accNo,
            disputedAmount: amount,
            channel: req.Channel,
            disputeReason: req.Reason);

        // If matching core transaction exists, mark as disputed
        var originalTx = await _adcRepository.GetByIdAsync(req.OriginalTransactionId, ct)
                      ?? await _adcRepository.GetByStanAndRrnAsync(req.OriginalStan, req.OriginalRrn, ct);
        originalTx?.MarkDisputed();

        await _reconRepository.AddDisputeAsync(claim, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new DisputeClaimResponse(
            claim.ClaimId,
            claim.OriginalTransactionId,
            claim.OriginalStan,
            claim.OriginalRrn,
            claim.AccountNumber.Value,
            claim.DisputedAmount.Amount,
            claim.Channel.ToString(),
            claim.DisputeReason,
            claim.Status.ToString(),
            claim.RectificationVoucherNumber,
            claim.ResolvedByOfficerId,
            claim.InvestigationNotes,
            claim.CreatedAtUtc,
            claim.ResolvedAtUtc);
    }

    public async Task<DisputeClaimResponse> HandleResolveDisputeRefundAsync(ResolveDisputeCommand command, CancellationToken ct = default)
    {
        var claim = await _reconRepository.GetDisputeByIdAsync(command.ClaimId, ct);
        if (claim is null)
            throw new ArgumentException($"Dispute Claim '{command.ClaimId}' not found.", nameof(command.ClaimId));

        var req = command.Request;

        // 1. Post Rectification Voucher crediting customer account (BRPD 1.4.1-vii)
        var account = await _accountRepository.GetByAccountNumberAsync(claim.AccountNumber, ct);
        if (account is null)
            throw new KeyNotFoundException($"Customer account '{claim.AccountNumber.Value}' not found.");

        account.Credit(claim.DisputedAmount, $"DISPUTE-REFUND-STAN:{claim.OriginalStan}-RRN:{claim.OriginalRrn}", isCustomerInduced: false);
        await _accountRepository.UpdateAsync(account, ct);

        var voucher = await _glService.PostPaymentRailTransferAsync(
            branchSol: account.BranchCode,
            originatingAccount: account.Id.Value,
            amount: claim.DisputedAmount.Amount,
            railType: claim.Channel.ToString(),
            refId: $"DSP-REF-{claim.OriginalStan}",
            makerUserId: req.CheckerUserId,
            checkerUserId: null,
            ct: ct);

        // 2. Resolve Dispute with strict STAN & RRN verification lock (BRPD 1.4.1-vii)
        claim.ResolveWithRefund(
            checkerUserId: req.CheckerUserId,
            rectificationVoucherNumber: voucher.VoucherNumber,
            providedStan: req.ProvidedStan,
            providedRrn: req.ProvidedRrn,
            investigationNotes: req.Notes);

        await _unitOfWork.SaveChangesAsync(ct);

        return new DisputeClaimResponse(
            claim.ClaimId,
            claim.OriginalTransactionId,
            claim.OriginalStan,
            claim.OriginalRrn,
            claim.AccountNumber.Value,
            claim.DisputedAmount.Amount,
            claim.Channel.ToString(),
            claim.DisputeReason,
            claim.Status.ToString(),
            claim.RectificationVoucherNumber,
            claim.ResolvedByOfficerId,
            claim.InvestigationNotes,
            claim.CreatedAtUtc,
            claim.ResolvedAtUtc);
    }
}
