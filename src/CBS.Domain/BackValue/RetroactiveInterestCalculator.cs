// ARCHITECTURAL INTENT: Retroactive Interest Differential Calculator domain service
// computing exact historical interest adjustments for value-dated transactions per BRPD Circular No. 12 (13 May 2024) Module 1 (1.3.4-ii).

using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.BackValue;

public static class RetroactiveInterestCalculator
{
    public sealed record RetroactiveInterestResult(
        decimal DifferentialAmount,
        AdjustmentDirection Direction,
        int DaysPast,
        decimal PrincipalAmount,
        decimal AnnualInterestRatePercent,
        string AccountingNarration);

    /// <summary>
    /// Computes the exact retroactive interest differential from Value Date to Booking Date.
    /// Formula: Delta Interest = Principal * (AnnualRate / 100) * (DaysPast / 365).
    /// </summary>
    public static RetroactiveInterestResult Calculate(
        decimal principalAmount,
        decimal annualInterestRatePercent,
        DateOnly valueDate,
        DateOnly bookingDate,
        DebitCredit transactionNature,
        bool isDepositAccount)
    {
        if (principalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(principalAmount), "Principal amount must be positive.");

        if (annualInterestRatePercent < 0m)
            throw new ArgumentOutOfRangeException(nameof(annualInterestRatePercent), "Annual interest rate cannot be negative.");

        if (valueDate > bookingDate)
        {
            throw new InvariantViolationException(
                "FutureValueDateProhibitedInvariant",
                "BRPD Circular 12/2024 (1.3.4-i)",
                $"Value Date '{valueDate:yyyy-MM-dd}' cannot be later than Booking Date '{bookingDate:yyyy-MM-dd}'.");
        }

        int daysPast = bookingDate.DayNumber - valueDate.DayNumber;
        if (daysPast == 0 || annualInterestRatePercent == 0m)
        {
            return new RetroactiveInterestResult(
                DifferentialAmount: 0m,
                Direction: AdjustmentDirection.CreditCustomer,
                DaysPast: daysPast,
                PrincipalAmount: principalAmount,
                AnnualInterestRatePercent: annualInterestRatePercent,
                AccountingNarration: "No retroactive interest adjustment required (zero days or zero interest rate).");
        }

        // Exact 365-day statutory daily interest convention for Bangladesh
        decimal rawDifferential = principalAmount * (annualInterestRatePercent / 100m) * ((decimal)daysPast / 365m);
        decimal differentialAmount = Math.Round(rawDifferential, 2, MidpointRounding.AwayFromZero);

        AdjustmentDirection direction;
        string narration;

        if (isDepositAccount)
        {
            if (transactionNature == DebitCredit.Credit)
            {
                // Customer deposited money in the past: Bank owes retroactive accrued interest to customer
                direction = AdjustmentDirection.CreditCustomer;
                narration = $"Retroactive deposit interest credited for {daysPast} days ({valueDate:yyyy-MM-dd} to {bookingDate:yyyy-MM-dd}) at {annualInterestRatePercent}% p.a.";
            }
            else
            {
                // Customer withdrew money in the past: Customer over-accrued interest, bank claws back interest
                direction = AdjustmentDirection.DebitCustomer;
                narration = $"Retroactive deposit interest clawback for {daysPast} days ({valueDate:yyyy-MM-dd} to {bookingDate:yyyy-MM-dd}) at {annualInterestRatePercent}% p.a.";
            }
        }
        else
        {
            // Loan / Financing Account
            if (transactionNature == DebitCredit.Credit)
            {
                // Borrower repaid loan in the past: Borrower overpaid loan interest, refund/credit borrower
                direction = AdjustmentDirection.CreditCustomer;
                narration = $"Retroactive loan interest refund for {daysPast} days ({valueDate:yyyy-MM-dd} to {bookingDate:yyyy-MM-dd}) at {annualInterestRatePercent}% p.a.";
            }
            else
            {
                // Loan disbursed in the past: Borrower owes additional loan interest
                direction = AdjustmentDirection.DebitCustomer;
                narration = $"Retroactive loan interest charge for {daysPast} days ({valueDate:yyyy-MM-dd} to {bookingDate:yyyy-MM-dd}) at {annualInterestRatePercent}% p.a.";
            }
        }

        return new RetroactiveInterestResult(
            DifferentialAmount: differentialAmount,
            Direction: direction,
            DaysPast: daysPast,
            PrincipalAmount: principalAmount,
            AnnualInterestRatePercent: annualInterestRatePercent,
            AccountingNarration: narration);
    }
}
