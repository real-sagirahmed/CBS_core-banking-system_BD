using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Remittance Instruments (Pay Order & Demand Draft) lifecycle,
// statutory NBR 15% VAT on commission, walk-in CDD enforcement, and dual-control issuance (BRPD Circular 12/2024 Module 1.5.6, 2.1, 3.11).
public class RemittanceInstrument : AggregateRoot<Guid>
{
    public string InstrumentNumber { get; private set; }
    public RemittanceInstrumentType Type { get; private set; }
    public string BranchCode { get; private set; }
    public string? PurchaserCif { get; private set; }
    public WalkInCustomerDetails? WalkInDetails { get; private set; }
    public string BeneficiaryName { get; private set; }
    public Money Amount { get; private set; }
    public Money Commission { get; private set; }
    public Money VatAmount { get; private set; }
    public Money TotalPayable { get; private set; }
    public RemittanceInstrumentStatus Status { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public string MakerUserId { get; private set; }
    public string CheckerUserId { get; private set; }
    public DateOnly? LiquidatedDate { get; private set; }
    public string? LiquidatedBranchCode { get; private set; }
    public string? StopReason { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? DuplicateInstrumentNumber { get; private set; }
    public string? IndemnityBondReference { get; private set; }

    public RemittanceInstrument(
        Guid id,
        string instrumentNumber,
        RemittanceInstrumentType type,
        string branchCode,
        Money amount,
        string beneficiaryName,
        string? purchaserCif,
        WalkInCustomerDetails? walkInDetails,
        string makerUserId,
        string checkerUserId,
        DateOnly issueDate) : base(id)
    {
        if (string.IsNullOrWhiteSpace(instrumentNumber)) throw new ArgumentException("Instrument number is required.", nameof(instrumentNumber));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(beneficiaryName)) throw new ArgumentException("Beneficiary name is required.", nameof(beneficiaryName));
        if (string.IsNullOrWhiteSpace(makerUserId)) throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));
        if (string.IsNullOrWhiteSpace(checkerUserId)) throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));
        ArgumentNullException.ThrowIfNull(amount, nameof(amount));

        if (amount.Amount <= 0)
        {
            throw new InvariantViolationException(
                "InvalidRemittanceAmountInvariant",
                "BRPD Circular 12/2024 (3.11.1-i)",
                $"Remittance instrument amount must be strictly positive (provided: {amount.Amount}).");
        }

        // Dual-Control 4-eyes principle
        if (makerUserId.Equals(checkerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                $"Maker '{makerUserId}' cannot authorize their own remittance instrument issuance.");
        }

        // Walk-in CDD enforcement for >= 50,000 BDT
        WalkInCustomerDetails.EnforceWalkInDueDiligence(amount, walkInDetails, purchaserCif);

        // Tiered Commission Calculation (BRPD 1.5.6-v, 3.11.1-i)
        decimal commissionAmt = CalculateTieredCommission(amount.Amount);
        var commission = Money.Bdt(commissionAmt);

        // Statutory 15% NBR VAT on Commission (BRPD 1.5.6-v)
        decimal vatAmt = Math.Round(commissionAmt * 0.15m, 2, MidpointRounding.AwayFromZero);
        var vat = Money.Bdt(vatAmt);

        InstrumentNumber = instrumentNumber.Trim().ToUpperInvariant();
        Type = type;
        BranchCode = branchCode.Trim();
        Amount = amount;
        BeneficiaryName = beneficiaryName.Trim();
        PurchaserCif = purchaserCif?.Trim();
        WalkInDetails = walkInDetails;
        Commission = commission;
        VatAmount = vat;
        TotalPayable = amount + commission + vat;
        Status = RemittanceInstrumentStatus.Issued;
        IssueDate = issueDate;
        MakerUserId = makerUserId.Trim();
        CheckerUserId = checkerUserId.Trim();
    }

    public static decimal CalculateTieredCommission(decimal principalAmount)
    {
        return principalAmount switch
        {
            <= 25000m => 25m,
            <= 100000m => 50m,
            <= 500000m => 100m,
            _ => 200m
        };
    }

    public void Liquidate(string presentingBranchCode, string tellerUserId, DateOnly liquidationDate)
    {
        if (string.IsNullOrWhiteSpace(presentingBranchCode)) throw new ArgumentException("Presenting branch code is required.", nameof(presentingBranchCode));
        if (string.IsNullOrWhiteSpace(tellerUserId)) throw new ArgumentException("Teller User ID is required.", nameof(tellerUserId));

        if (Status == RemittanceInstrumentStatus.Liquidated)
        {
            throw new InvariantViolationException(
                "DuplicateLiquidationBlockedInvariant",
                "BRPD Circular 12/2024 (2.1.2-i)",
                $"Remittance instrument '{InstrumentNumber}' is already liquidated on {LiquidatedDate} at branch {LiquidatedBranchCode}. Duplicate encashment prohibited.");
        }

        if (Status == RemittanceInstrumentStatus.Stopped)
        {
            throw new InvariantViolationException(
                "StoppedInstrumentLiquidationBlockedInvariant",
                "BRPD Circular 12/2024 (2.1.1-iii)",
                $"Remittance instrument '{InstrumentNumber}' is stopped (Reason: {StopReason}). Encashment is strictly blocked.");
        }

        if (Status == RemittanceInstrumentStatus.Cancelled)
        {
            throw new InvariantViolationException(
                "CancelledInstrumentLiquidationBlockedInvariant",
                "BRPD Circular 12/2024 (2.1.2-ii)",
                $"Remittance instrument '{InstrumentNumber}' was previously cancelled and refunded. Encashment is prohibited.");
        }

        Status = RemittanceInstrumentStatus.Liquidated;
        LiquidatedDate = liquidationDate;
        LiquidatedBranchCode = presentingBranchCode.Trim();
    }

    public void StopPayment(string reason, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Stop payment reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != RemittanceInstrumentStatus.Issued)
        {
            throw new InvariantViolationException(
                "InvalidInstrumentStatusForStopPaymentInvariant",
                "BRPD Circular 12/2024 (2.1.1)",
                $"Cannot place stop payment on instrument '{InstrumentNumber}' with status '{Status}'. Only 'Issued' instruments can be stopped.");
        }

        Status = RemittanceInstrumentStatus.Stopped;
        StopReason = reason.Trim();
    }

    public Money CancelAndRefund(string reason, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != RemittanceInstrumentStatus.Issued && Status != RemittanceInstrumentStatus.Stopped)
        {
            throw new InvariantViolationException(
                "InvalidInstrumentStatusForCancellationInvariant",
                "BRPD Circular 12/2024 (2.1.2)",
                $"Cannot cancel instrument '{InstrumentNumber}' with status '{Status}'. Only 'Issued' or 'Stopped' instruments can be cancelled.");
        }

        Status = RemittanceInstrumentStatus.Cancelled;
        CancellationReason = reason.Trim();

        // Commission and VAT are non-refundable statutory earnings of the bank and NBR
        return Amount;
    }

    public void IssueDuplicate(string newInstrumentNumber, string indemnityBondReference, string supervisorUserId)
    {
        if (string.IsNullOrWhiteSpace(newInstrumentNumber)) throw new ArgumentException("New instrument number is required.", nameof(newInstrumentNumber));
        if (string.IsNullOrWhiteSpace(indemnityBondReference)) throw new ArgumentException("Indemnity bond reference is required.", nameof(indemnityBondReference));
        if (string.IsNullOrWhiteSpace(supervisorUserId)) throw new ArgumentException("Supervisor User ID is required.", nameof(supervisorUserId));

        if (Status != RemittanceInstrumentStatus.Stopped)
        {
            throw new InvariantViolationException(
                "DuplicateIssuanceRequiresStopPaymentInvariant",
                "BRPD Circular 12/2024 (2.1.1-iv)",
                $"A duplicate instrument can only be issued against a stopped instrument backed by an indemnity bond (Current status: {Status}).");
        }

        Status = RemittanceInstrumentStatus.DuplicateIssued;
        DuplicateInstrumentNumber = newInstrumentNumber.Trim().ToUpperInvariant();
        IndemnityBondReference = indemnityBondReference.Trim();
    }
}
