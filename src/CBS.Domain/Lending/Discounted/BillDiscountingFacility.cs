using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Discounted;

// ARCHITECTURAL INTENT: Discounted Loans & Inland/Foreign Bill Purchase (IBP/FBP)
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 4.6.
// Enforces upfront discount deduction, daily straight-line unearned interest amortization to P&L,
// unearned interest rebate reversal on premature retirement, and automatic liquidation upon drawee realization.
public class BillDiscountingFacility : AggregateRoot<AccountNumber>
{
    public CifNumber Cif { get; private set; }
    public string BillReferenceNumber { get; private set; }
    public string DraweeBankRouting { get; private set; }
    public string DraweeBankName { get; private set; }
    public string BranchCode { get; private set; }

    public Money BillFaceValue { get; private set; }
    public decimal DiscountRatePercent { get; private set; }
    public int TenorDays { get; private set; }

    public Money UpfrontDiscountAmount { get; private set; }
    public Money NetDisbursedAmount { get; private set; }
    public Money UnearnedDiscountBalance { get; private set; }
    public Money RecognizedPnLInterest { get; private set; }

    public DateOnly PurchaseDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public BillDiscountingStatus Status { get; private set; }

    #pragma warning disable CS8618
    protected BillDiscountingFacility() : base(null!) { }
    #pragma warning restore CS8618

    public BillDiscountingFacility(
        AccountNumber facilityAccountNumber,
        CifNumber cif,
        string billReferenceNumber,
        string draweeBankRouting,
        string draweeBankName,
        string branchCode,
        Money billFaceValue,
        decimal discountRatePercent,
        int tenorDays,
        DateOnly purchaseDate) : base(facilityAccountNumber)
    {
        ArgumentNullException.ThrowIfNull(facilityAccountNumber, nameof(facilityAccountNumber));
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        ArgumentNullException.ThrowIfNull(billFaceValue, nameof(billFaceValue));

        if (string.IsNullOrWhiteSpace(billReferenceNumber))
            throw new ArgumentException("Bill reference number is required.", nameof(billReferenceNumber));
        if (string.IsNullOrWhiteSpace(draweeBankRouting) || draweeBankRouting.Trim().Length != 9)
            throw new ArgumentException("Drawee bank routing must be 9 digits.", nameof(draweeBankRouting));
        if (tenorDays <= 0 || tenorDays > 365)
            throw new ArgumentOutOfRangeException(nameof(tenorDays), "Discounted bill tenor must be between 1 and 365 days.");
        if (discountRatePercent <= 0 || discountRatePercent >= 100)
            throw new ArgumentOutOfRangeException(nameof(discountRatePercent), "Discount rate must be strictly between 0% and 100%.");

        Cif = cif;
        BillReferenceNumber = billReferenceNumber.Trim();
        DraweeBankRouting = draweeBankRouting.Trim();
        DraweeBankName = draweeBankName.Trim();
        BranchCode = branchCode.Trim();

        BillFaceValue = billFaceValue;
        DiscountRatePercent = discountRatePercent;
        TenorDays = tenorDays;
        PurchaseDate = purchaseDate;
        MaturityDate = purchaseDate.AddDays(tenorDays);

        // Calculate Upfront Discount: FaceValue * (Rate/100) * (Tenor / 365)
        decimal upfrontAmount = Math.Round(billFaceValue.Amount * (discountRatePercent / 100m) * (tenorDays / 365m), billFaceValue.Currency.DecimalPlaces, MidpointRounding.AwayFromZero);
        UpfrontDiscountAmount = new Money(upfrontAmount, billFaceValue.Currency);
        NetDisbursedAmount = billFaceValue - UpfrontDiscountAmount;
        UnearnedDiscountBalance = UpfrontDiscountAmount;
        RecognizedPnLInterest = Money.Zero(billFaceValue.Currency);

        Status = BillDiscountingStatus.Discounted;
    }

    // 1. INVARIANT: Daily Straight-Line Amortization into P&L (BRPD 4.6.2-i)
    public Money AmortizeDailyUnearnedInterest(int elapsedDays)
    {
        if (Status != BillDiscountingStatus.Discounted && Status != BillDiscountingStatus.MaturedPendingRealization)
            return Money.Zero(BillFaceValue.Currency);

        if (elapsedDays <= 0 || UnearnedDiscountBalance.Amount <= 0)
            return Money.Zero(BillFaceValue.Currency);

        decimal dailyAmortization = UpfrontDiscountAmount.Amount / TenorDays;
        decimal totalAmortizable = Math.Min(dailyAmortization * elapsedDays, UnearnedDiscountBalance.Amount);
        totalAmortizable = Math.Round(totalAmortizable, BillFaceValue.Currency.DecimalPlaces, MidpointRounding.AwayFromZero);

        var amortizedMoney = new Money(totalAmortizable, BillFaceValue.Currency);
        UnearnedDiscountBalance -= amortizedMoney;
        RecognizedPnLInterest += amortizedMoney;

        return amortizedMoney;
    }

    // 2. INVARIANT: Early Pay-off & Unearned Interest Rebate Reversal (BRPD 4.6.2-ii)
    public Money EarlyRetireAndRebate()
    {
        if (Status != BillDiscountingStatus.Discounted)
        {
            throw new InvariantViolationException(
                "InvalidBillStateForEarlyRetirementInvariant",
                "BRPD Circular 12/2024 (4.6.2-ii)",
                $"Cannot early-retire bill '{BillReferenceNumber}' in status '{Status}'.");
        }

        Money rebateAmount = UnearnedDiscountBalance;
        UnearnedDiscountBalance = Money.Zero(BillFaceValue.Currency);
        Status = BillDiscountingStatus.EarlyRetiredRebated;

        return rebateAmount;
    }

    // 3. INVARIANT: Automatic Liquidation via Drawee Realization (BRPD 4.6.4)
    public void LiquidateFromDraweeRealization(Money realizedAmount)
    {
        ArgumentNullException.ThrowIfNull(realizedAmount, nameof(realizedAmount));

        if (realizedAmount < BillFaceValue)
        {
            throw new InvariantViolationException(
                "InsufficientDraweeRealizationInvariant",
                "BRPD Circular 12/2024 (4.6.4-i)",
                $"Realized payment ({realizedAmount}) is less than Bill Face Value ({BillFaceValue}).");
        }

        // Recognize any remaining unearned discount at final liquidation
        if (UnearnedDiscountBalance.Amount > 0)
        {
            RecognizedPnLInterest += UnearnedDiscountBalance;
            UnearnedDiscountBalance = Money.Zero(BillFaceValue.Currency);
        }

        Status = BillDiscountingStatus.RealizedDraweePaid;
    }
}
