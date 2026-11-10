using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.TermDeposits;
using CBS.Domain.ValueObjects;
using FluentAssertions;

namespace CBS.Domain.UnitTests;

public class TermDepositAndFdrTests
{
    private static readonly AccountNumber FdrAcc = AccountNumber.From("0101101000010");
    private static readonly AccountNumber OperativeAcc = AccountNumber.From("0101101000029");

    [Fact]
    public void TermDepositContract_ThrowsException_WhenTenorIsLessThan3Months()
    {
        var act = () => new TermDepositContract(
            "FDR-001",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(100000m),
            tenorMonths: 2, // Less than 3 months
            annualInterestRate: 8.50m,
            startDate: new DateOnly(2026, 11, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Term Deposit tenor '2' months is below regulatory minimum of 3 months*");
    }

    [Fact]
    public void TermDepositContract_DailyProductAccrual_CalculatesAccurately()
    {
        var contract = new TermDepositContract(
            "FDR-002",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(1000000m), // 1 Million BDT
            tenorMonths: 12,
            annualInterestRate: 9.00m, // 9.00%
            startDate: new DateOnly(2026, 1, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);

        // Accrue for 30 days (1 Jan to 31 Jan)
        // Formula: (1,000,000 * 9.00 * 30) / (360 * 100) = 7,500.00 BDT
        var accrued = contract.AccrueDailyInterest(new DateOnly(2026, 1, 31));

        accrued.Amount.Should().Be(7500.00m);
        contract.AccruedInterestProvision.Amount.Should().Be(7500.00m);
    }

    [Fact]
    public void TermDepositContract_ExecuteMaturityRollover_CapitalizesNetInterest_WhenCompounding()
    {
        var contract = new TermDepositContract(
            "FDR-003",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(100000m), // 100k BDT
            tenorMonths: 12,
            annualInterestRate: 10.00m,
            startDate: new DateOnly(2025, 1, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);

        // Maturity Date is 2026-01-01 (365 days)
        // Gross Interest: (100,000 * 10.00 * 365) / 36,000 = 10,138.89 BDT
        // With TIN: 10% tax = 1,013.89 BDT -> Net = 9,125.00 BDT
        var result = contract.ExecuteMaturityRollover(prevailingInterestRate: 10.50m, hasTin: true);

        result.Instruction.Should().Be(TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);
        result.NetInterestCapitalized.Should().Be(9125.00m);
        result.RolledPrincipal.Should().Be(109125.00m); // 100k + 9,125
        result.PushedToOperativeAccount.Should().Be(0m);
        result.NewMaturityDate.Should().Be(new DateOnly(2027, 1, 1));
        contract.PrincipalAmount.Amount.Should().Be(109125.00m);
        contract.AnnualInterestRate.Should().Be(10.50m);
    }

    [Fact]
    public void TermDepositContract_PrematureEncashment_YieldsZeroInterest_WhenUnder3Months()
    {
        var contract = new TermDepositContract(
            "FDR-004",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(500000m),
            tenorMonths: 12,
            annualInterestRate: 8.00m,
            startDate: new DateOnly(2026, 1, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalOnly);

        // Encash after 45 days (< 90 days / 3 months)
        var result = contract.CalculateAndApplyPrematureEncashment(
            encashmentDate: new DateOnly(2026, 2, 15),
            penaltyMarkdownPercentage: 2.00m,
            hasTin: true);

        result.DaysElapsed.Should().Be(45);
        result.AppliedInterestRate.Should().Be(0m);
        result.GrossInterest.Should().Be(0m);
        result.NetInterest.Should().Be(0m);
        result.PrincipalPayout.Should().Be(500000m);
        result.TotalPayoutAmount.Should().Be(500000m); // Only principal returned
        contract.Status.Should().Be(TermDepositStatus.PrematurelyClosed);
    }

    [Fact]
    public void TermDepositContract_PrematureEncashment_AppliesPenaltyAndTax_WhenOver3Months()
    {
        var contract = new TermDepositContract(
            "FDR-005",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(500000m),
            tenorMonths: 12,
            annualInterestRate: 8.00m,
            startDate: new DateOnly(2026, 1, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalOnly);

        // Accrue for 6 months (180 days)
        contract.AccrueDailyInterest(new DateOnly(2026, 6, 30));

        // Premature encashment after 180 days with 2% penalty markdown -> Applied rate = 6.00%
        // Gross: (500,000 * 6.00 * 180) / 36,000 = 15,000 BDT
        // Without TIN: 15% tax = 2,250 BDT -> Net = 12,750 BDT
        var result = contract.CalculateAndApplyPrematureEncashment(
            encashmentDate: new DateOnly(2026, 6, 30),
            penaltyMarkdownPercentage: 2.00m,
            hasTin: false);

        result.DaysElapsed.Should().Be(180);
        result.AppliedInterestRate.Should().Be(6.00m);
        result.GrossInterest.Should().Be(15000m);
        result.WithholdingTax.Should().Be(2250m);
        result.NetInterest.Should().Be(12750m);
        result.TotalPayoutAmount.Should().Be(512750m); // 500k + 12,750
        contract.Status.Should().Be(TermDepositStatus.PrematurelyClosed);
    }

    [Fact]
    public void TermDepositContract_PrematureEncashment_ThrowsException_WhenUnderActiveLien()
    {
        var contract = new TermDepositContract(
            "FDR-006",
            FdrAcc,
            OperativeAcc,
            Money.Bdt(1000000m),
            tenorMonths: 12,
            annualInterestRate: 9.00m,
            startDate: new DateOnly(2026, 1, 1),
            maturityInstruction: TermDepositMaturityInstruction.AutoRenewPrincipalAndInterest);

        // CAD marks lien for loan against FDR
        contract.MarkLien(Money.Bdt(800000m), "LN-AGAINST-FDR-2026-001");
        contract.IsLienMarked.Should().BeTrue();
        contract.Status.Should().Be(TermDepositStatus.LienMarked);

        // Attempt premature encashment while under active lien
        var act = () => contract.CalculateAndApplyPrematureEncashment(
            encashmentDate: new DateOnly(2026, 6, 1),
            penaltyMarkdownPercentage: 2.00m,
            hasTin: true);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*FDR Contract 'FDR-006' is under active lien (LN-AGAINST-FDR-2026-001*Premature encashment strictly prohibited until CAD lien release*");

        // Release lien
        contract.ReleaseLien("LOAN-REPAID-FULL");
        contract.IsLienMarked.Should().BeFalse();
        contract.Status.Should().Be(TermDepositStatus.Active);

        // Now premature encashment succeeds
        var actAfterRelease = () => contract.CalculateAndApplyPrematureEncashment(
            encashmentDate: new DateOnly(2026, 6, 1),
            penaltyMarkdownPercentage: 2.00m,
            hasTin: true);

        actAfterRelease.Should().NotThrow();
    }
}
