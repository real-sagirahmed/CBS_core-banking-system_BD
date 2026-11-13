// ARCHITECTURAL INTENT: Telegraphic/Telephonic Transfer (TT) Aggregate Root
// enforcing cryptographic test numbers, maker-checker dual custody, sanction screening,
// telephonic callback logging, and responding advice settlement per Bangladesh Bank BRPD Circular No. 12 (3.13, 1.6.5).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TelegraphicTransfers;

public class TelegraphicTransfer : AggregateRoot<string>
{
    public string TtNumber { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public string IssuingBranchCode { get; private set; }
    public string DraweeBranchCode { get; private set; }
    public Money Amount { get; private set; }
    public string PayeeName { get; private set; }
    public string? PayeeAccountNumber { get; private set; }
    public string? RemitterName { get; private set; }
    public string? RemitterAccountNumber { get; private set; }
    public string? TransactionPurpose { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal MessageChargeAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public string TtTestKey { get; private set; }
    public TtStatus Status { get; private set; }
    public bool SanctionScreeningPassed { get; private set; }
    public string MakerUserId { get; private set; }
    public string CheckerUserId { get; private set; }
    public DateTime? AdviceTransmittedAtUtc { get; private set; }
    public string? AdviceTransmittedByUserId { get; private set; }
    public DateTime? TelephonicallyVerifiedAtUtc { get; private set; }
    public string? TelephonicVerifiedByOfficer { get; private set; }
    public string? TelephonicContactNumber { get; private set; }
    public string? TelephonicNotes { get; private set; }
    public DateOnly? RespondedDate { get; private set; }
    public string? RespondingAdviceNumber { get; private set; }
    public string? RespondingOfficerUserId { get; private set; }
    public string? RespondingVoucherNumber { get; private set; }
    public string? DestinationAccountOrSundryGl { get; private set; }
    public DateOnly? CancelledDate { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public string? CancellationRemarks { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private TelegraphicTransfer() : base(string.Empty) { }
#pragma warning restore CS8618

    public TelegraphicTransfer(
        string ttId,
        string ttNumber,
        DateOnly issueDate,
        string issuingBranchCode,
        string draweeBranchCode,
        Money amount,
        string payeeName,
        string? payeeAccountNumber,
        string? remitterName,
        string? remitterAccountNumber,
        string? transactionPurpose,
        bool sanctionScreeningPassed,
        string makerUserId,
        string checkerUserId) : base(ttId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ttId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ttNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuingBranchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(draweeBranchCode);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(payeeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(makerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkerUserId);

        if (amount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be positive.");

        if (string.Equals(makerUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "TtMakerCheckerInvariant",
                "BRPD 1.3.18",
                "Telegraphic Transfer issuance requires dual-control approval. Maker and Checker cannot be identical.");
        }

        if (string.Equals(issuingBranchCode.Trim(), draweeBranchCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "TtInterBranchRequiredInvariant",
                "BRPD 3.13.1-ii",
                "Telegraphic Transfer is strictly an inter-branch transfer. Issuing branch and Payee/Drawee branch cannot be identical.");
        }

        if (!sanctionScreeningPassed)
        {
            throw new InvariantViolationException(
                "TtSanctionScreeningRequiredInvariant",
                "BRPD 3.13.1-vi",
                "Telegraphic Transfer prohibited. Remitter or Payee has pending or positive match on UNSCR / local sanction list.");
        }

        var (comm, msgCharge, vat, _) = TtFeeCalculator.Calculate(amount.Amount);

        TtNumber = ttNumber.Trim().ToUpperInvariant();
        IssueDate = issueDate;
        IssuingBranchCode = issuingBranchCode.Trim().ToUpperInvariant();
        DraweeBranchCode = draweeBranchCode.Trim().ToUpperInvariant();
        Amount = amount;
        PayeeName = payeeName.Trim();
        PayeeAccountNumber = payeeAccountNumber?.Trim();
        RemitterName = remitterName?.Trim();
        RemitterAccountNumber = remitterAccountNumber?.Trim();
        TransactionPurpose = transactionPurpose?.Trim();
        CommissionAmount = comm;
        MessageChargeAmount = msgCharge;
        VatAmount = vat;
        SanctionScreeningPassed = sanctionScreeningPassed;
        MakerUserId = makerUserId.Trim().ToUpperInvariant();
        CheckerUserId = checkerUserId.Trim().ToUpperInvariant();
        Status = TtStatus.Issued;
        TtTestKey = TtTestKeyCalculator.GenerateTestKey(issueDate, IssuingBranchCode, DraweeBranchCode, amount.Amount);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAdviceTransmitted(string transmittedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transmittedByUserId);

        if (Status != TtStatus.Issued)
        {
            throw new InvariantViolationException(
                "TtAdviceTransmissionStatusInvariant",
                "BRPD 3.13.1-iv",
                $"Cannot transmit advice for TT '{TtNumber}' with status '{Status}'. Only 'Issued' transfers can transmit advice.");
        }

        Status = TtStatus.AdviceTransmitted;
        AdviceTransmittedAtUtc = DateTime.UtcNow;
        AdviceTransmittedByUserId = transmittedByUserId.Trim().ToUpperInvariant();
    }

    public void LogTelephonicVerification(
        string receivingOfficerUserId,
        string verifiedWithOfficerName,
        string contactNumber,
        string notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receivingOfficerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedWithOfficerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);

        if (Status == TtStatus.Responded || Status == TtStatus.Cancelled)
        {
            throw new InvariantViolationException(
                "TtTelephonicVerificationStatusInvariant",
                "BRPD 3.13.2-i",
                $"Cannot verify telephonically for TT '{TtNumber}' with terminal status '{Status}'.");
        }

        Status = TtStatus.TelephonicallyVerified;
        TelephonicallyVerifiedAtUtc = DateTime.UtcNow;
        TelephonicVerifiedByOfficer = receivingOfficerUserId.Trim().ToUpperInvariant();
        TelephonicContactNumber = contactNumber.Trim();
        TelephonicNotes = $"Verified with {verifiedWithOfficerName.Trim()}: {notes.Trim()}";
    }

    public void RespondAndPay(
        DateOnly respondedDate,
        string respondingOfficerUserId,
        string respondingVoucherNumber,
        string presentedTestKey,
        string destinationAccountOrSundryGl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(respondingOfficerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(respondingVoucherNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentedTestKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationAccountOrSundryGl);

        if (Status == TtStatus.Responded)
        {
            throw new InvariantViolationException(
                "TtAlreadyRespondedInvariant",
                "BRPD 3.13.2-ii",
                $"Telegraphic Transfer '{TtNumber}' has already been responded and credited under advice '{RespondingAdviceNumber}'.");
        }

        if (Status == TtStatus.Cancelled)
        {
            throw new InvariantViolationException(
                "TtCancelledCannotRespondInvariant",
                "BRPD 3.13.2-ii",
                $"Cannot respond to cancelled Telegraphic Transfer '{TtNumber}'.");
        }

        if (!TtTestKeyCalculator.VerifyTestKey(presentedTestKey, IssueDate, IssuingBranchCode, DraweeBranchCode, Amount.Amount))
        {
            throw new InvariantViolationException(
                "TtTestKeyMismatchInvariant",
                "BRPD 3.13.2-ii",
                $"Drawee branch cannot honor Telegraphic Transfer '{TtNumber}'. Presented Test Key '{presentedTestKey}' does not match drawing test key.");
        }

        Status = TtStatus.Responded;
        RespondedDate = respondedDate;
        RespondingOfficerUserId = respondingOfficerUserId.Trim().ToUpperInvariant();
        RespondingVoucherNumber = respondingVoucherNumber.Trim().ToUpperInvariant();
        DestinationAccountOrSundryGl = destinationAccountOrSundryGl.Trim().ToUpperInvariant();
        RespondingAdviceNumber = $"TT-RESP-{DraweeBranchCode}-{respondedDate:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
    }

    public void Cancel(
        DateOnly cancelledDate,
        string cancelledByUserId,
        string remarks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelledByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(remarks);

        if (Status == TtStatus.Responded)
        {
            throw new InvariantViolationException(
                "TtCancellationStatusInvariant",
                "BRPD 3.13.1",
                $"Telegraphic Transfer '{TtNumber}' cannot be cancelled because it has already been responded and paid by drawee branch.");
        }

        if (Status == TtStatus.Cancelled)
        {
            throw new InvariantViolationException(
                "TtCancellationStatusInvariant",
                "BRPD 3.13.1",
                $"Telegraphic Transfer '{TtNumber}' is already cancelled.");
        }

        Status = TtStatus.Cancelled;
        CancelledDate = cancelledDate;
        CancelledByUserId = cancelledByUserId.Trim().ToUpperInvariant();
        CancellationRemarks = remarks.Trim();
    }
}
