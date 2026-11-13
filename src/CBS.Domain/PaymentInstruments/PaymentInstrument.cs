// ARCHITECTURAL INTENT: Payment Order (PO) and Demand Draft (DD) Lifecycle Aggregate Root
// enforcing BFIU Walk-in KYC thresholds, Cryptographic Test Keys, Joint Custody,
// and Statutory Reversal/Encashment controls under Bangladesh Bank BRPD Circular No. 12 (1.5.6, 3.11, 3.12).

using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.PaymentInstruments;

public class PaymentInstrument : AggregateRoot<string>
{
    public const decimal BfiuWalkInThreshold = 50_000m;

    public PaymentInstrumentType InstrumentType { get; private set; }
    public string InstrumentNumber { get; private set; }
    public string IssuingBranchCode { get; private set; }
    public string DraweeBranchCode { get; private set; }
    public Money Amount { get; private set; }
    public string PayeeName { get; private set; }
    public string RemitterName { get; private set; }
    public string? RemitterAccountNumber { get; private set; }
    public bool IsWalkInCustomer { get; private set; }
    public string? WalkInNid { get; private set; }
    public string? WalkInPhotoId { get; private set; }
    public string? WalkInMobile { get; private set; }
    public string TransactionPurpose { get; private set; }
    public PaymentInstrumentStatus Status { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public string IssuedByUserId { get; private set; }
    public string AuthorizedByUserId { get; private set; }
    public string? DdTestKey { get; private set; }
    public bool DrawingAdviceTransmitted { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public string? PaidByUserId { get; private set; }
    public string? PaidVoucherNumber { get; private set; }
    public DateOnly? CancelledDate { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public string? CancellationRemarks { get; private set; }
    public decimal? CancellationFeeDeducted { get; private set; }
    public string? PoliceGdNumber { get; private set; }
    public string? IndemnityBondNumber { get; private set; }
    public string? DuplicateOfInstrumentId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private PaymentInstrument() : base(string.Empty) { }
#pragma warning restore CS8618

    public PaymentInstrument(
        string instrumentId,
        PaymentInstrumentType instrumentType,
        string instrumentNumber,
        string issuingBranchCode,
        string? draweeBranchCode,
        Money amount,
        string payeeName,
        string remitterName,
        string? remitterAccountNumber,
        bool isWalkInCustomer,
        string? walkInNid,
        string? walkInPhotoId,
        string? walkInMobile,
        string transactionPurpose,
        DateOnly issueDate,
        string issuedByUserId,
        string authorizedByUserId,
        string? duplicateOfInstrumentId = null,
        string? policeGdNumber = null,
        string? indemnityBondNumber = null) : base(instrumentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuingBranchCode);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(payeeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remitterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionPurpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuedByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizedByUserId);

        if (amount.Amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Instrument amount must be positive.");

        if (string.Equals(issuedByUserId.Trim(), authorizedByUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "InstrumentMakerCheckerInvariant",
                "BRPD 1.3.18",
                "Payment instrument issuance requires dual-custody approval. Maker and Checker cannot be identical.");
        }

        if (isWalkInCustomer && amount.Amount >= BfiuWalkInThreshold)
        {
            if (string.IsNullOrWhiteSpace(walkInNid) || string.IsNullOrWhiteSpace(walkInMobile))
            {
                throw new InvariantViolationException(
                    "BfiuWalkInKycRequiredInvariant",
                    "BRPD 1.5.6-vi / BFIU Circular",
                    $"Walk-in customer purchasing instrument of BDT {amount.Amount:N2} (>= {BfiuWalkInThreshold:N2}) must provide verified National ID (NID) and mobile number.");
            }
        }

        if (instrumentType == PaymentInstrumentType.DemandDraft)
        {
            if (string.IsNullOrWhiteSpace(draweeBranchCode))
            {
                throw new InvariantViolationException(
                    "DemandDraftDraweeBranchRequiredInvariant",
                    "BRPD 3.12.1-ii",
                    "Demand Draft requires a designated Drawee/Payee branch for inter-branch drawing.");
            }

            if (string.Equals(issuingBranchCode.Trim(), draweeBranchCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvariantViolationException(
                    "DemandDraftInterBranchInvariant",
                    "BRPD 3.12.1-ii",
                    "Demand Draft is strictly for inter-branch remittance. Issuing and Drawee branches cannot be the same.");
            }
        }

        var (comm, vat, _) = InstrumentCommissionCalculator.Calculate(instrumentType, amount.Amount);

        InstrumentType = instrumentType;
        InstrumentNumber = instrumentNumber.Trim().ToUpperInvariant();
        IssuingBranchCode = issuingBranchCode.Trim().ToUpperInvariant();
        DraweeBranchCode = (draweeBranchCode ?? issuingBranchCode).Trim().ToUpperInvariant();
        Amount = amount;
        PayeeName = payeeName.Trim();
        RemitterName = remitterName.Trim();
        RemitterAccountNumber = remitterAccountNumber?.Trim();
        IsWalkInCustomer = isWalkInCustomer;
        WalkInNid = walkInNid?.Trim();
        WalkInPhotoId = walkInPhotoId?.Trim();
        WalkInMobile = walkInMobile?.Trim();
        TransactionPurpose = transactionPurpose.Trim();
        Status = PaymentInstrumentStatus.Issued;
        CommissionAmount = comm;
        VatAmount = vat;
        IssueDate = issueDate;
        IssuedByUserId = issuedByUserId.Trim().ToUpperInvariant();
        AuthorizedByUserId = authorizedByUserId.Trim().ToUpperInvariant();
        DuplicateOfInstrumentId = duplicateOfInstrumentId;
        PoliceGdNumber = policeGdNumber?.Trim();
        IndemnityBondNumber = indemnityBondNumber?.Trim();
        CreatedAtUtc = DateTime.UtcNow;

        if (instrumentType == PaymentInstrumentType.DemandDraft)
        {
            DdTestKey = DdTestKeyCalculator.GenerateTestKey(issueDate, IssuingBranchCode, DraweeBranchCode, amount.Amount);
            DrawingAdviceTransmitted = true;
        }
    }

    public void MarkPaid(
        DateOnly paidDate,
        string paidByUserId,
        string voucherNumber,
        string? presentedTestKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paidByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(voucherNumber);

        if (Status != PaymentInstrumentStatus.Issued)
        {
            throw new InvariantViolationException(
                "InstrumentPaymentStatusInvariant",
                "BRPD 1.5.6-i",
                $"Payment instrument '{InstrumentNumber}' cannot be paid because current status is '{Status}'. Only 'Issued' instruments are payable.");
        }

        if (InstrumentType == PaymentInstrumentType.DemandDraft)
        {
            if (string.IsNullOrWhiteSpace(presentedTestKey) || !string.Equals(presentedTestKey.Trim(), DdTestKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvariantViolationException(
                    "DemandDraftTestKeyMismatchInvariant",
                    "BRPD 3.12.2-i",
                    $"Drawee branch cannot honor Demand Draft '{InstrumentNumber}'. Test Key '{presentedTestKey}' does not match drawing advice test key.");
            }
        }

        Status = PaymentInstrumentStatus.Paid;
        PaidDate = paidDate;
        PaidByUserId = paidByUserId.Trim().ToUpperInvariant();
        PaidVoucherNumber = voucherNumber.Trim().ToUpperInvariant();
    }

    public void Cancel(
        DateOnly cancelledDate,
        string cancelledByUserId,
        string remarks,
        decimal cancellationFee = 0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelledByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(remarks);

        if (Status != PaymentInstrumentStatus.Issued)
        {
            throw new InvariantViolationException(
                "InstrumentCancellationStatusInvariant",
                "BRPD 1.5.6-i, 3.11.2-i",
                $"Payment instrument '{InstrumentNumber}' cannot be cancelled because status is '{Status}'. Only 'Issued' instruments can be cancelled.");
        }

        Status = PaymentInstrumentStatus.Cancelled;
        CancelledDate = cancelledDate;
        CancelledByUserId = cancelledByUserId.Trim().ToUpperInvariant();
        CancellationRemarks = remarks.Trim();
        CancellationFeeDeducted = cancellationFee >= 0m ? cancellationFee : 0m;
    }

    public void MarkStopped(string reason, string stoppedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(stoppedByUserId);

        if (Status != PaymentInstrumentStatus.Issued)
        {
            throw new InvariantViolationException(
                "InstrumentStopPaymentStatusInvariant",
                "BRPD 3.11.1-vi",
                $"Payment instrument '{InstrumentNumber}' cannot be stopped because status is '{Status}'.");
        }

        Status = PaymentInstrumentStatus.Stopped;
    }

    public static PaymentInstrument CreateDuplicate(
        string newInstrumentId,
        string newInstrumentNumber,
        PaymentInstrument original,
        string policeGdNumber,
        string indemnityBondNumber,
        string issuedByUserId,
        string authorizedByUserId)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNullOrWhiteSpace(policeGdNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(indemnityBondNumber);

        if (original.Status != PaymentInstrumentStatus.Stopped && original.Status != PaymentInstrumentStatus.Issued)
        {
            throw new InvariantViolationException(
                "DuplicateInstrumentIndemnityInvariant",
                "BRPD 1.5.6-i",
                $"Cannot issue duplicate for instrument '{original.InstrumentNumber}' with status '{original.Status}'.");
        }

        // Original instrument is permanently stopped / marked replaced
        original.Status = PaymentInstrumentStatus.DuplicateIssued;

        return new PaymentInstrument(
            instrumentId: newInstrumentId,
            instrumentType: original.InstrumentType,
            instrumentNumber: newInstrumentNumber,
            issuingBranchCode: original.IssuingBranchCode,
            draweeBranchCode: original.DraweeBranchCode,
            amount: new Money(original.Amount.Amount, original.Amount.Currency),
            payeeName: original.PayeeName,
            remitterName: original.RemitterName,
            remitterAccountNumber: original.RemitterAccountNumber,
            isWalkInCustomer: original.IsWalkInCustomer,
            walkInNid: original.WalkInNid,
            walkInPhotoId: original.WalkInPhotoId,
            walkInMobile: original.WalkInMobile,
            transactionPurpose: $"Duplicate issued against lost instrument {original.InstrumentNumber}",
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            issuedByUserId: issuedByUserId,
            authorizedByUserId: authorizedByUserId,
            duplicateOfInstrumentId: original.Id,
            policeGdNumber: policeGdNumber,
            indemnityBondNumber: indemnityBondNumber);
    }
}
