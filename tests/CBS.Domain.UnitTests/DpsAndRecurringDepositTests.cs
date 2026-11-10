using CBS.Domain.Deposits;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class DpsAndRecurringDepositTests
{
    private static readonly AccountNumber DpsAcc = AccountNumber.From("0101101000010");
    private static readonly AccountNumber LinkedCasaAcc = AccountNumber.From("0101101000029");
    private static readonly CifNumber TestCif = new("CIF-CUST-99001");

    [Fact]
    public void DpsAccount_Creation_GeneratesAccurateInstallmentScheduleMatchingTenor()
    {
        // 5-year tenor = 60 installments
        var dps = new DpsAccount(
            id: "DPS-001",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(5000),
            tenorYears: 5,
            contractedAnnualRate: 8.50m,
            prematureSavingsPenaltyRate: 4.00m,
            debitDayOfMonth: 10,
            startDate: new DateOnly(2025, 1, 10));

        dps.TotalInstallments.Should().Be(60);
        dps.Installments.Should().HaveCount(60);
        dps.Status.Should().Be(DpsStatus.Active);
        dps.MaturityDate.Should().Be(new DateOnly(2030, 1, 10));

        var first = dps.Installments.First();
        first.InstallmentNumber.Should().Be(1);
        first.DueDate.Should().Be(new DateOnly(2025, 1, 10));
        first.Status.Should().Be(DpsInstallmentStatus.Pending);

        var last = dps.Installments.Last();
        last.InstallmentNumber.Should().Be(60);
        last.DueDate.Should().Be(new DateOnly(2029, 12, 10));
    }

    [Fact]
    public void DpsAccount_ProcessInstallmentPayment_AccruesCompoundInterestAndResetsDefaults()
    {
        var dps = new DpsAccount(
            id: "DPS-002",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(2000),
            tenorYears: 3,
            contractedAnnualRate: 9.00m,
            prematureSavingsPenaltyRate: 4.00m,
            debitDayOfMonth: 5,
            startDate: new DateOnly(2025, 1, 5));

        dps.ProcessInstallmentPayment(1, Money.Bdt(2000), new DateOnly(2025, 1, 5));

        dps.PaidInstallmentsCount.Should().Be(1);
        dps.TotalPrincipalPaid.Amount.Should().Be(2000m);
        // Monthly interest on 2000 at 9% = 2000 * 0.09 / 12 = 15.00 BDT
        dps.TotalInterestAccrued.Amount.Should().Be(15.00m);

        var ins1 = dps.Installments.First(i => i.InstallmentNumber == 1);
        ins1.Status.Should().Be(DpsInstallmentStatus.Paid);
        ins1.PaidDate.Should().Be(new DateOnly(2025, 1, 5));
    }

    [Fact]
    public void DpsAccount_ConsecutiveMissedInstallments_FourStrikes_TriggersDefaultSuspended()
    {
        var dps = new DpsAccount(
            id: "DPS-003",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(1000),
            tenorYears: 2,
            contractedAnnualRate: 8.00m,
            prematureSavingsPenaltyRate: 3.50m,
            debitDayOfMonth: 1,
            startDate: new DateOnly(2025, 1, 1));

        // Evaluate on 2025-04-20 (Jan, Feb, Mar, Apr installments are past grace period)
        dps.RecordMissedInstallment(new DateOnly(2025, 4, 20));

        dps.ConsecutiveDefaultCount.Should().Be(4);
        dps.Status.Should().Be(DpsStatus.DefaultSuspended);

        // Assert payment is blocked while suspended
        var act = () => dps.ProcessInstallmentPayment(1, Money.Bdt(1000), new DateOnly(2025, 4, 21));
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DefaultSuspended*");
    }

    [Fact]
    public void DpsAccount_PrematureEncashment_BeforeOneYear_YieldsZeroInterest()
    {
        var dps = new DpsAccount(
            id: "DPS-004",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(5000),
            tenorYears: 5,
            contractedAnnualRate: 8.50m,
            prematureSavingsPenaltyRate: 4.00m,
            debitDayOfMonth: 10,
            startDate: new DateOnly(2025, 1, 10));

        // Pay 6 installments (6 months < 12 months)
        for (int i = 1; i <= 6; i++)
        {
            dps.ProcessInstallmentPayment(i, Money.Bdt(5000), new DateOnly(2025, i, 10));
        }

        var quote = dps.CalculatePrematureEncashment(new DateOnly(2025, 7, 15), hasTaxIdentificationNumber: true);

        // BRPD 3.8.5: Zero interest before 1 year
        quote.GrossInterest.Amount.Should().Be(0m);
        quote.WithholdingTax.Amount.Should().Be(0m);
        quote.TotalPrincipal.Amount.Should().Be(30000m);
        quote.NetPayableAmount.Amount.Should().Be(30000m);
        quote.EffectiveRateApplied.Should().Be(0.00m);
    }

    [Fact]
    public void DpsAccount_PrematureEncashment_AfterOneYear_RecalculatesSavingsPenaltyRateWithTINTax()
    {
        var dps = new DpsAccount(
            id: "DPS-005",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(10000),
            tenorYears: 3,
            contractedAnnualRate: 9.00m,
            prematureSavingsPenaltyRate: 4.00m,
            debitDayOfMonth: 1,
            startDate: new DateOnly(2024, 1, 1));

        // Pay 15 installments (15 months >= 12 months)
        for (int i = 1; i <= 15; i++)
        {
            dps.ProcessInstallmentPayment(i, Money.Bdt(10000), new DateOnly(2024, 1, 1).AddMonths(i - 1));
        }

        // 1. With e-TIN (10% tax)
        var quoteWithTin = dps.CalculatePrematureEncashment(new DateOnly(2025, 4, 1), hasTaxIdentificationNumber: true);
        quoteWithTin.TotalPrincipal.Amount.Should().Be(150000m);
        quoteWithTin.GrossInterest.Amount.Should().BeGreaterThan(0m);
        quoteWithTin.EffectiveRateApplied.Should().Be(4.00m);
        // Withholding tax = 10%
        quoteWithTin.WithholdingTax.Amount.Should().Be(Math.Round(quoteWithTin.GrossInterest.Amount * 0.10m, 2));

        // 2. Without e-TIN (15% tax)
        var quoteWithoutTin = dps.CalculatePrematureEncashment(new DateOnly(2025, 4, 1), hasTaxIdentificationNumber: false);
        quoteWithoutTin.WithholdingTax.Amount.Should().Be(Math.Round(quoteWithoutTin.GrossInterest.Amount * 0.15m, 2));
    }

    [Fact]
    public void DpsAccount_CadActiveLienLock_BlocksPrematureEncashmentUntilReleased()
    {
        var dps = new DpsAccount(
            id: "DPS-006",
            dpsAccountNumber: DpsAcc,
            linkedCasaAccountNumber: LinkedCasaAcc,
            customerCif: TestCif,
            monthlyInstallmentAmount: Money.Bdt(5000),
            tenorYears: 3,
            contractedAnnualRate: 8.50m,
            prematureSavingsPenaltyRate: 4.00m,
            debitDayOfMonth: 10,
            startDate: new DateOnly(2024, 1, 10));

        for (int i = 1; i <= 12; i++)
        {
            dps.ProcessInstallmentPayment(i, Money.Bdt(5000), new DateOnly(2024, 1, 10).AddMonths(i - 1));
        }

        // CAD marks lien for Demand Loan
        dps.MarkLien("LIEN-DLAD-01", "Collateral for Demand Loan LOAN-8899");
        dps.IsLienMarked.Should().BeTrue();
        dps.Status.Should().Be(DpsStatus.LienMarked);

        // Invariant: Encashment blocked
        var act = () => dps.CalculatePrematureEncashment(new DateOnly(2025, 2, 1), hasTaxIdentificationNumber: true);
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*active CAD lien*");

        // Release lien
        dps.ReleaseLien("LIEN-DLAD-01");
        dps.IsLienMarked.Should().BeFalse();
        dps.Status.Should().Be(DpsStatus.Active);

        // Now encashment is permitted
        var quote = dps.CalculatePrematureEncashment(new DateOnly(2025, 2, 1), hasTaxIdentificationNumber: true);
        quote.NetPayableAmount.Amount.Should().BeGreaterThan(0m);
    }
}
