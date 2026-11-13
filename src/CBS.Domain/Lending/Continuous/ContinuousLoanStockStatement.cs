// ARCHITECTURAL INTENT: Continuous Loan Stock & Book Debts Statement Entity
// Computes dynamic eligible Drawing Power (DP) based on paid stock valuation and qualified receivables
// strictly excluding book debts older than 90 days per Bangladesh Bank BRPD Circular No. 12 (4.1.4, 4.1.6).

using System;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Continuous;

public class ContinuousLoanStockStatement
{
    public string StatementId { get; private set; }
    public string LoanAccountNumber { get; private set; }
    public DateOnly StatementDate { get; private set; }
    public DateOnly AsOfPeriodDate { get; private set; }

    public Money RawMaterialsValue { get; private set; }
    public Money WorkInProgressValue { get; private set; }
    public Money FinishedGoodsValue { get; private set; }
    public Money TotalStockValue { get; private set; }
    public Money CreditorsForGoods { get; private set; }
    public Money PaidStockValue { get; private set; }

    public Money BookDebtsUnder90Days { get; private set; }
    public Money BookDebtsOver90Days { get; private set; }

    public decimal StockMarginPercent { get; private set; }
    public decimal BookDebtsMarginPercent { get; private set; }

    public Money EligibleStockDrawingPower { get; private set; }
    public Money EligibleBookDebtsDrawingPower { get; private set; }
    public Money TotalCalculatedDrawingPower { get; private set; }

    public string SubmittedByBorrowerRepresentative { get; private set; }
    public string VettedByOfficerUserId { get; private set; }
    public DateTime VettedAtUtc { get; private set; }

#pragma warning disable CS8618
    private ContinuousLoanStockStatement() { }
#pragma warning restore CS8618

    public ContinuousLoanStockStatement(
        string statementId,
        string loanAccountNumber,
        DateOnly statementDate,
        DateOnly asOfPeriodDate,
        Money rawMaterialsValue,
        Money workInProgressValue,
        Money finishedGoodsValue,
        Money creditorsForGoods,
        Money bookDebtsUnder90Days,
        Money bookDebtsOver90Days,
        decimal stockMarginPercent,
        decimal bookDebtsMarginPercent,
        string submittedByBorrowerRepresentative,
        string vettedByOfficerUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loanAccountNumber);
        ArgumentNullException.ThrowIfNull(rawMaterialsValue);
        ArgumentNullException.ThrowIfNull(workInProgressValue);
        ArgumentNullException.ThrowIfNull(finishedGoodsValue);
        ArgumentNullException.ThrowIfNull(creditorsForGoods);
        ArgumentNullException.ThrowIfNull(bookDebtsUnder90Days);
        ArgumentNullException.ThrowIfNull(bookDebtsOver90Days);
        ArgumentException.ThrowIfNullOrWhiteSpace(submittedByBorrowerRepresentative);
        ArgumentException.ThrowIfNullOrWhiteSpace(vettedByOfficerUserId);

        if (stockMarginPercent < 0m || stockMarginPercent >= 100m)
            throw new ArgumentOutOfRangeException(nameof(stockMarginPercent), "Stock margin must be between 0% and 100%.");

        if (bookDebtsMarginPercent < 0m || bookDebtsMarginPercent >= 100m)
            throw new ArgumentOutOfRangeException(nameof(bookDebtsMarginPercent), "Book debts margin must be between 0% and 100%.");

        StatementId = statementId.Trim().ToUpperInvariant();
        LoanAccountNumber = loanAccountNumber.Trim();
        StatementDate = statementDate;
        AsOfPeriodDate = asOfPeriodDate;

        RawMaterialsValue = rawMaterialsValue;
        WorkInProgressValue = workInProgressValue;
        FinishedGoodsValue = finishedGoodsValue;
        CreditorsForGoods = creditorsForGoods;

        var grossStock = rawMaterialsValue.Amount + workInProgressValue.Amount + finishedGoodsValue.Amount;
        TotalStockValue = Money.Bdt(grossStock);

        var paidStock = Math.Max(0m, grossStock - creditorsForGoods.Amount);
        PaidStockValue = Money.Bdt(paidStock);

        BookDebtsUnder90Days = bookDebtsUnder90Days;
        BookDebtsOver90Days = bookDebtsOver90Days; // Ineligible per BRPD 4.1.4

        StockMarginPercent = stockMarginPercent;
        BookDebtsMarginPercent = bookDebtsMarginPercent;

        var eligibleStockDp = Math.Round(paidStock * (1m - (stockMarginPercent / 100m)), 2, MidpointRounding.AwayFromZero);
        var eligibleDebtsDp = Math.Round(bookDebtsUnder90Days.Amount * (1m - (bookDebtsMarginPercent / 100m)), 2, MidpointRounding.AwayFromZero);

        EligibleStockDrawingPower = Money.Bdt(eligibleStockDp);
        EligibleBookDebtsDrawingPower = Money.Bdt(eligibleDebtsDp);
        TotalCalculatedDrawingPower = Money.Bdt(eligibleStockDp + eligibleDebtsDp);

        SubmittedByBorrowerRepresentative = submittedByBorrowerRepresentative.Trim();
        VettedByOfficerUserId = vettedByOfficerUserId.Trim().ToUpperInvariant();
        VettedAtUtc = DateTime.UtcNow;
    }
}
