using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Remittance;
using CBS.Domain.Remittance.ValueObjects;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Remittance;

// ARCHITECTURAL INTENT: Command Handler orchestrating Inward SWIFT MT103 Processing,
// Wage Earners' 2.5% Cash Incentive Calculation & CASA Crediting, Outward Quota Checking,
// and Bangladesh Bank Incentive Claim Batching (BRPD Circular 12/2024 Module 2.2).
public class ForeignRemittanceCommandHandler
{
    private readonly IForeignRemittanceRepository _remittanceRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ForeignRemittanceCommandHandler(
        IForeignRemittanceRepository remittanceRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _remittanceRepository = remittanceRepository ?? throw new ArgumentNullException(nameof(remittanceRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ForeignRemittanceResponse> ProcessInwardAsync(ProcessInwardRemittanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var beneficiaryAccNo = AccountNumber.From(request.BeneficiaryAccountNumber);
        var beneficiaryAcc = await _accountRepository.GetByAccountNumberAsync(beneficiaryAccNo, ct)
            ?? throw new KeyNotFoundException($"Beneficiary CASA account '{request.BeneficiaryAccountNumber}' was not found.");

        var foreignCcy = Currency.FromCode(request.ForeignCurrency);
        var foreignMoney = new Money(request.ForeignAmount, foreignCcy);

        string refNo = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? $"INW-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : request.TransactionReference.Trim().ToUpperInvariant();

        var swiftPayload = new SwiftMt103Payload(
            refNo,
            "CRED",
            DateOnly.FromDateTime(DateTime.UtcNow),
            foreignCcy,
            request.ForeignAmount,
            request.SenderName,
            request.SenderBankBic ?? "UNKNOWN",
            request.SenderBankBic ?? "UNKNOWN",
            beneficiaryAccNo.Value,
            request.BeneficiaryName,
            request.PurposeOfRemittance ?? "WAGE EARNER REMITTANCE",
            request.ChargeParty);

        var txn = ForeignRemittanceTransaction.CreateInward(
            Guid.NewGuid(),
            refNo,
            request.RemittanceType,
            request.SenderName,
            request.SenderCountry,
            request.SenderBankBic,
            request.BeneficiaryName,
            beneficiaryAccNo,
            beneficiaryAcc.Cif,
            foreignMoney,
            request.ExchangeRate,
            request.ChargeParty,
            request.MakerUserId,
            DateTime.UtcNow,
            swiftPayload,
            request.SanctionStatus ?? SanctionStatus.Clean,
            request.PurposeOfRemittance);

        await _remittanceRepository.AddAsync(txn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    public async Task<ForeignRemittanceResponse> AuthorizeInwardAsync(Guid id, AuthorizeInwardRemittanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var txn = await _remittanceRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Foreign remittance transaction '{id}' was not found.");

        if (txn.BeneficiaryAccountNumber is null)
            throw new InvariantViolationException("MissingBeneficiaryAccount", "BRPD 2.2.2", "Beneficiary account is missing for inward remittance.");

        var beneficiaryAcc = await _accountRepository.GetByAccountNumberAsync(txn.BeneficiaryAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Beneficiary CASA account '{txn.BeneficiaryAccountNumber.Value}' was not found.");

        // Execute domain authorization & invariant checks
        txn.AuthorizeInwardRemittance(request.CheckerUserId, DateTime.UtcNow);

        // Credit CASA account with Converted BDT + 2.5% Government Incentive
        string narration = txn.IncentiveAmount.Amount > 0
            ? $"Inward foreign remittance {txn.TransactionReference} ({txn.ForeignAmount.Amount} {txn.ForeignAmount.Currency.Code}) incl 2.5% govt incentive {txn.IncentiveAmount.Amount} BDT"
            : $"Inward foreign remittance {txn.TransactionReference} ({txn.ForeignAmount.Amount} {txn.ForeignAmount.Currency.Code})";

        beneficiaryAcc.Credit(txn.TotalDisbursedBdtAmount, narration);

        await _accountRepository.UpdateAsync(beneficiaryAcc, ct);
        await _remittanceRepository.UpdateAsync(txn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    public async Task<ForeignRemittanceResponse> InitiateOutwardAsync(InitiateOutwardRemittanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var senderAccNo = AccountNumber.From(request.SenderDebitAccountNumber);
        var senderAcc = await _accountRepository.GetByAccountNumberAsync(senderAccNo, ct)
            ?? throw new KeyNotFoundException($"Sender CASA account '{request.SenderDebitAccountNumber}' was not found.");

        var foreignCcy = Currency.FromCode(request.ForeignCurrency);
        var foreignMoney = new Money(request.ForeignAmount, foreignCcy);

        decimal requiredBdt = Math.Round(request.ForeignAmount * request.ExchangeRate, 2, MidpointRounding.AwayFromZero);
        if (senderAcc.Balance.Amount < requiredBdt)
        {
            throw new InvariantViolationException(
                "InsufficientFundsForOutwardRemittance",
                "BRPD Circular 12/2024 (2.2.1-i)",
                $"Sender account has insufficient balance ({senderAcc.Balance.Amount:N2} BDT) for outward remittance requiring {requiredBdt:N2} BDT.");
        }

        string refNo = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? $"OUT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : request.TransactionReference.Trim().ToUpperInvariant();

        var swiftPayload = new SwiftMt103Payload(
            refNo,
            "CRED",
            DateOnly.FromDateTime(DateTime.UtcNow),
            foreignCcy,
            request.ForeignAmount,
            request.SenderName,
            "OURBANKBD",
            request.SenderBankBic ?? "FOREIGNBIC",
            request.BeneficiaryName,
            request.BeneficiaryName,
            request.PurposeOfRemittance ?? "OUTWARD EDUCATION/MEDICAL REMITTANCE",
            request.ChargeParty);

        var txn = ForeignRemittanceTransaction.CreateOutward(
            Guid.NewGuid(),
            refNo,
            request.RemittanceType,
            request.SenderName,
            request.SenderCountry,
            request.SenderBankBic,
            request.BeneficiaryName,
            senderAccNo,
            senderAcc.Cif,
            foreignMoney,
            request.ExchangeRate,
            request.ChargeParty,
            request.MakerUserId,
            DateTime.UtcNow,
            swiftPayload,
            request.SanctionStatus ?? SanctionStatus.Clean,
            request.PurposeOfRemittance,
            request.QuotaReference,
            request.QuotaLimitAnnualBdt,
            request.QuotaUtilizedAnnualBdt);

        await _remittanceRepository.AddAsync(txn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    public async Task<ForeignRemittanceResponse> AuthorizeOutwardAsync(Guid id, AuthorizeOutwardRemittanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var txn = await _remittanceRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Foreign remittance transaction '{id}' was not found.");

        if (txn.BeneficiaryAccountNumber is null)
            throw new InvariantViolationException("MissingSenderAccount", "BRPD 2.2.1", "Sender debit account is missing for outward remittance.");

        var senderAcc = await _accountRepository.GetByAccountNumberAsync(txn.BeneficiaryAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Sender account '{txn.BeneficiaryAccountNumber.Value}' was not found.");

        // Domain authorization & cancellation lock
        txn.AuthorizeOutwardRemittance(request.CheckerUserId, DateTime.UtcNow);

        // Debit sender account for BDT equivalent
        senderAcc.Debit(txn.ConvertedBdtAmount, $"Outward foreign remittance {txn.TransactionReference} ({txn.ForeignAmount.Amount} {txn.ForeignAmount.Currency.Code})");

        await _accountRepository.UpdateAsync(senderAcc, ct);
        await _remittanceRepository.UpdateAsync(txn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    public async Task<ForeignRemittanceResponse> CancelAsync(Guid id, CancelRemittanceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var txn = await _remittanceRepository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Foreign remittance transaction '{id}' was not found.");

        txn.Cancel(request.Reason, request.RequestedByUserId);

        await _remittanceRepository.UpdateAsync(txn, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(txn);
    }

    public async Task<IncentiveClaimBatchResponse> BatchIncentivesAsync(BatchIncentiveClaimRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var eligibleTxns = await _remittanceRepository.GetUnclaimedIncentivesAsync(ct);
        if (eligibleTxns.Count == 0)
        {
            throw new InvariantViolationException(
                "EmptyIncentiveClaimBatchInvariant",
                "BRPD Circular 12/2024 (2.2.3)",
                "No eligible disbursed wage earner incentives available to batch.");
        }

        decimal totalIncentiveAmount = eligibleTxns.Sum(t => t.IncentiveAmount.Amount);
        var totalMoney = Money.Bdt(totalIncentiveAmount);

        string batchNo = $"BB-INC-{request.ClaimPeriod.Replace("-", "")}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var batch = new GovernmentIncentiveClaimBatch(
            Guid.NewGuid(),
            batchNo,
            request.ClaimPeriod,
            eligibleTxns.Count,
            totalMoney,
            request.PreparedByUserId,
            DateTime.UtcNow);

        foreach (var txn in eligibleTxns)
        {
            txn.MarkIncentiveBatched(batch.Id);
            await _remittanceRepository.UpdateAsync(txn, ct);
        }

        await _remittanceRepository.AddClaimBatchAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBatchToResponse(batch);
    }

    public async Task<IncentiveClaimBatchResponse> SubmitClaimBatchAsync(Guid batchId, SubmitIncentiveClaimRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = await _remittanceRepository.GetClaimBatchByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Government incentive claim batch '{batchId}' was not found.");

        batch.SubmitToBangladeshBank(request.CentralBankSubmissionReference, DateTime.UtcNow);

        await _remittanceRepository.UpdateClaimBatchAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapBatchToResponse(batch);
    }

    public async Task<ForeignRemittanceResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var txn = await _remittanceRepository.GetByIdAsync(id, ct);
        return txn is null ? null : MapToResponse(txn);
    }

    public async Task<IReadOnlyList<ForeignRemittanceResponse>> GetInwardRegisterAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var txns = await _remittanceRepository.GetInwardRegisterAsync(fromUtc, toUtc, ct);
        return txns.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<IncentiveClaimBatchResponse>> GetAllClaimBatchesAsync(CancellationToken ct = default)
    {
        var batches = await _remittanceRepository.GetAllClaimBatchesAsync(ct);
        return batches.Select(MapBatchToResponse).ToList();
    }

    private static ForeignRemittanceResponse MapToResponse(ForeignRemittanceTransaction txn)
    {
        return new ForeignRemittanceResponse(
            txn.Id,
            txn.TransactionReference,
            txn.Direction.ToString(),
            txn.RemittanceType.ToString(),
            txn.SenderName,
            txn.SenderCountry,
            txn.SenderBankBic,
            txn.BeneficiaryName,
            txn.BeneficiaryAccountNumber?.Value,
            txn.ForeignAmount.Currency.Code,
            txn.ForeignAmount.Amount,
            txn.ExchangeRate,
            txn.ConvertedBdtAmount.Amount,
            txn.IncentiveRate,
            txn.IncentiveAmount.Amount,
            txn.TotalDisbursedBdtAmount.Amount,
            txn.ChargeParty.ToString(),
            txn.SanctionStatus.ToString(),
            txn.Status.ToString(),
            txn.IncentiveStatus.ToString(),
            txn.CentralBankClaimBatchId,
            txn.PurposeOfRemittance,
            txn.QuotaReference,
            txn.QuotaLimitAnnualBdt,
            txn.QuotaUtilizedAnnualBdt,
            txn.MakerUserId,
            txn.CheckerUserId,
            txn.CreatedAtUtc,
            txn.AuthorizedAtUtc,
            txn.DisbursedAtUtc,
            txn.CancellationReason);
    }

    private static IncentiveClaimBatchResponse MapBatchToResponse(GovernmentIncentiveClaimBatch batch)
    {
        return new IncentiveClaimBatchResponse(
            batch.Id,
            batch.BatchNumber,
            batch.ClaimPeriod,
            batch.TotalTransactionCount,
            batch.TotalClaimedIncentiveBdt.Amount,
            batch.Status.ToString(),
            batch.PreparedByUserId,
            batch.BatchedAtUtc,
            batch.SubmittedAtUtc,
            batch.ReimbursedAtUtc,
            batch.CentralBankReference);
    }
}
