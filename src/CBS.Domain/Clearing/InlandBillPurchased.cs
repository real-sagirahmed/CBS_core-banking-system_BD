// ARCHITECTURAL INTENT: Aggregate Root managing Inland Bills Purchased (IBP) and Discounting,
// margin retention, discounting interest calculation, and forced demand loan crystallization
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 2 & 4.

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Clearing;

public sealed class InlandBillPurchased : AggregateRoot<Guid>
{
    public string IbpReferenceNumber { get; private set; } = null!;
    public string CustomerAccountNumber { get; private set; } = null!;
    public string CustomerCif { get; private set; } = null!;
    public string DrawerName { get; private set; } = null!;
    public string DraweeBankName { get; private set; } = null!;
    public string InstrumentNumber { get; private set; } = null!;
    public Money FaceValue { get; private set; } = null!;
    public decimal MarginRatePercent { get; private set; }
    public Money MarginRetained { get; private set; } = null!;
    public decimal DiscountRateAnnualPercent { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public int TenorDays { get; private set; }
    public Money DiscountInterest { get; private set; } = null!;
    public Money DocumentationFee { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money DisbursedAmount { get; private set; } = null!;
    public InlandBillPurchasedStatus Status { get; private set; }
    public string PurchasedByOfficerUserId { get; private set; } = null!;
    public string ApprovedByManagerUserId { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    // --- FORCED DEMAND LOAN CRYSTALLIZATION ---
    public string? ForcedLoanAccountNumber { get; private set; }
    public Money? CrystallizedOverdueAmount { get; private set; }
    public DateOnly? CrystallizationDate { get; private set; }
    public decimal? PenaltyInterestRatePercent { get; private set; }

    #pragma warning disable CS8618
    private InlandBillPurchased() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public InlandBillPurchased(
        Guid id,
        string ibpReferenceNumber,
        string customerAccountNumber,
        string customerCif,
        string drawerName,
        string draweeBankName,
        string instrumentNumber,
        Money faceValue,
        decimal marginRatePercent,
        decimal discountRateAnnualPercent,
        DateOnly purchaseDate,
        DateOnly maturityDate,
        string purchasedByOfficerUserId,
        string approvedByManagerUserId,
        decimal documentationFeeAmount = 500m)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(ibpReferenceNumber))
            throw new ArgumentException("IBP reference number is required.", nameof(ibpReferenceNumber));
        if (string.IsNullOrWhiteSpace(customerAccountNumber))
            throw new ArgumentException("Customer account number is required.", nameof(customerAccountNumber));
        if (string.IsNullOrWhiteSpace(customerCif))
            throw new ArgumentException("Customer CIF is required.", nameof(customerCif));
        if (string.IsNullOrWhiteSpace(drawerName))
            throw new ArgumentException("Drawer name is required.", nameof(drawerName));
        if (string.IsNullOrWhiteSpace(draweeBankName))
            throw new ArgumentException("Drawee bank name is required.", nameof(draweeBankName));
        if (string.IsNullOrWhiteSpace(instrumentNumber))
            throw new ArgumentException("Instrument number is required.", nameof(instrumentNumber));
        ArgumentNullException.ThrowIfNull(faceValue, nameof(faceValue));
        if (faceValue.Amount <= 0)
            throw new ArgumentException("Face value must be positive.", nameof(faceValue));
        if (marginRatePercent is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(marginRatePercent), "Margin rate must be between 0% and 50%.");
        if (discountRateAnnualPercent is <= 0 or > 30)
            throw new ArgumentOutOfRangeException(nameof(discountRateAnnualPercent), "Discount rate must be between 0% and 30%.");
        if (maturityDate <= purchaseDate)
            throw new ArgumentException("Maturity date must be strictly after purchase date.", nameof(maturityDate));
        if (string.IsNullOrWhiteSpace(purchasedByOfficerUserId))
            throw new ArgumentException("Purchased by officer user ID is required.", nameof(purchasedByOfficerUserId));
        if (string.IsNullOrWhiteSpace(approvedByManagerUserId))
            throw new ArgumentException("Approved by manager user ID is required.", nameof(approvedByManagerUserId));
        if (string.Equals(purchasedByOfficerUserId.Trim(), approvedByManagerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "IBP purchase officer and approving manager user IDs must be strictly distinct.",
                "BRPD Circular No. 12 (13 May 2024) Module 4"
            );
        }

        IbpReferenceNumber = ibpReferenceNumber.Trim();
        CustomerAccountNumber = customerAccountNumber.Trim();
        CustomerCif = customerCif.Trim();
        DrawerName = drawerName.Trim();
        DraweeBankName = draweeBankName.Trim();
        InstrumentNumber = instrumentNumber.Trim();
        FaceValue = faceValue;
        MarginRatePercent = marginRatePercent;
        DiscountRateAnnualPercent = discountRateAnnualPercent;
        PurchaseDate = purchaseDate;
        MaturityDate = maturityDate;
        PurchasedByOfficerUserId = purchasedByOfficerUserId.Trim();
        ApprovedByManagerUserId = approvedByManagerUserId.Trim();
        CreatedAtUtc = DateTime.UtcNow;

        TenorDays = (maturityDate.ToDateTime(TimeOnly.MinValue) - purchaseDate.ToDateTime(TimeOnly.MinValue)).Days;

        // Calculations:
        // 1. Margin Retained = FaceValue * MarginRate%
        var margin = Math.Round(faceValue.Amount * (marginRatePercent / 100m), 2);
        MarginRetained = new Money(margin, faceValue.Currency);

        // 2. Upfront Discount Interest = (FaceValue * Rate% * Days) / 365
        var interest = Math.Round((faceValue.Amount * (discountRateAnnualPercent / 100m) * TenorDays) / 365m, 2);
        DiscountInterest = new Money(interest, faceValue.Currency);

        // 3. Documentation Fee + 15% VAT
        var docFee = Math.Round(documentationFeeAmount, 2);
        var vat = Math.Round(docFee * 0.15m, 2);
        DocumentationFee = new Money(docFee, faceValue.Currency);
        VatAmount = new Money(vat, faceValue.Currency);

        // 4. Net Disbursed = FaceValue - Margin - Interest - DocFee - VAT
        var totalDeductions = margin + interest + docFee + vat;
        if (totalDeductions >= faceValue.Amount)
        {
            throw new InvariantViolationException(
                "IbpExcessiveDeductionsInvariant",
                $"Total deductions ({totalDeductions} BDT) cannot exceed or equal bill face value ({faceValue.Amount} BDT).",
                "BRPD Circular No. 12 Module 4"
            );
        }

        DisbursedAmount = new Money(faceValue.Amount - totalDeductions, faceValue.Currency);
        Status = InlandBillPurchasedStatus.Purchased;
    }

    public void SettleAtMaturity(DateOnly settlementDate, string checkerUserId)
    {
        if (Status != InlandBillPurchasedStatus.Purchased)
            throw new InvalidOperationException($"Cannot settle IBP in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        Status = InlandBillPurchasedStatus.SettledAtMaturity;
    }

    public void CrystallizeForcedLoan(
        string forcedLoanAccountNumber,
        DateOnly crystallizationDate,
        decimal penaltyRatePercent,
        string checkerUserId)
    {
        if (Status != InlandBillPurchasedStatus.Purchased && Status != InlandBillPurchasedStatus.DishonouredAtMaturity)
            throw new InvalidOperationException($"Cannot crystallize IBP in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(forcedLoanAccountNumber))
            throw new ArgumentException("Forced loan account number is required.", nameof(forcedLoanAccountNumber));
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));
        if (penaltyRatePercent < 0)
            throw new ArgumentException("Penalty rate cannot be negative.");

        ForcedLoanAccountNumber = forcedLoanAccountNumber.Trim();
        CrystallizationDate = crystallizationDate;
        PenaltyInterestRatePercent = penaltyRatePercent;

        // Forced loan amount equals face value minus margin previously held
        var forcedAmount = FaceValue.Amount - MarginRetained.Amount;
        CrystallizedOverdueAmount = new Money(forcedAmount, FaceValue.Currency);

        Status = InlandBillPurchasedStatus.CrystallizedToForcedLoan;
    }
}
