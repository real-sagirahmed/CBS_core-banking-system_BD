using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using FluentAssertions;

namespace CBS.Domain.UnitTests;

public class StandingInstructionAndSweepTests
{
    private static readonly AccountNumber Acc1 = AccountNumber.From("0101101000010");
    private static readonly AccountNumber Acc2 = AccountNumber.From("0101101000029");

    [Fact]
    public void StandingInstruction_ThrowsException_WhenSourceAndTargetAreIdentical()
    {
        var act = () => new StandingInstruction(
            "SI-001",
            Acc1,
            Acc1, // Same account
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Monthly,
            Money.Bdt(5000m),
            Money.Bdt(10m),
            new DateOnly(2026, 11, 10));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Source account and target account for a Standing Instruction cannot be identical*");
    }

    [Fact]
    public void StandingInstruction_ThrowsException_WhenTransferAmountIsZeroOrNegative()
    {
        var act = () => new StandingInstruction(
            "SI-002",
            Acc1,
            Acc2,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Monthly,
            Money.Bdt(0m),
            Money.Bdt(10m),
            new DateOnly(2026, 11, 10));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Standing Instruction transfer amount must be strictly positive*");
    }

    [Fact]
    public void StandingInstruction_SuspendsAutomatically_WhenMaxFailuresReached()
    {
        var si = new StandingInstruction(
            "SI-003",
            Acc1,
            Acc2,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Daily,
            Money.Bdt(1000m),
            Money.Bdt(5m),
            new DateOnly(2026, 11, 1),
            maxRetryFailures: 3);

        si.Status.Should().Be(StandingInstructionStatus.Active);

        si.RecordExecutionFailure(new DateOnly(2026, 11, 1), "Insufficient funds");
        si.ConsecutiveFailureCount.Should().Be(1);
        si.Status.Should().Be(StandingInstructionStatus.Active);

        si.RecordExecutionFailure(new DateOnly(2026, 11, 2), "Insufficient funds");
        si.ConsecutiveFailureCount.Should().Be(2);
        si.Status.Should().Be(StandingInstructionStatus.Active);

        si.RecordExecutionFailure(new DateOnly(2026, 11, 3), "Insufficient funds");
        si.ConsecutiveFailureCount.Should().Be(3);
        si.Status.Should().Be(StandingInstructionStatus.SuspendedDueToFailures);
        si.PauseReason.Should().Contain("Suspended after 3 consecutive failures");
    }

    [Fact]
    public void SweepAccountRule_ThrowsException_WhenUpperThresholdIsNotGreaterThanLower()
    {
        var act = () => new SweepAccountRule(
            "SWP-001",
            Acc1,
            Acc2,
            upperThreshold: Money.Bdt(20000m),
            lowerThreshold: Money.Bdt(30000m), // Invalid: Lower > Upper
            minimumSweepAmount: Money.Bdt(1000m));

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Upper threshold*must be strictly greater than lower threshold*");
    }

    [Fact]
    public void SweepAccountRule_EvaluatesSweepOut_WhenPrimaryHasSurplus()
    {
        var rule = new SweepAccountRule(
            "SWP-002",
            Acc1,
            Acc2,
            upperThreshold: Money.Bdt(100000m),
            lowerThreshold: Money.Bdt(20000m),
            minimumSweepAmount: Money.Bdt(5000m));

        // Primary account has 125,000 BDT -> Surplus is 25,000 BDT
        var (direction, amount) = rule.EvaluateSweep(125000m);

        direction.Should().Be(SweepDirection.SweepOut);
        amount.Should().Be(25000m);
    }

    [Fact]
    public void SweepAccountRule_EvaluatesSweepIn_WhenPrimaryHasDeficit()
    {
        var rule = new SweepAccountRule(
            "SWP-003",
            Acc1,
            Acc2,
            upperThreshold: Money.Bdt(100000m),
            lowerThreshold: Money.Bdt(20000m),
            minimumSweepAmount: Money.Bdt(5000m));

        // Primary account has 8,000 BDT -> Deficit below 20,000 BDT is 12,000 BDT
        var (direction, amount) = rule.EvaluateSweep(8000m);

        direction.Should().Be(SweepDirection.SweepIn);
        amount.Should().Be(12000m);
    }

    [Fact]
    public void SweepAccountRule_ReturnsNoAction_WhenBalanceWithinBandOrBelowMinSweep()
    {
        var rule = new SweepAccountRule(
            "SWP-004",
            Acc1,
            Acc2,
            upperThreshold: Money.Bdt(100000m),
            lowerThreshold: Money.Bdt(20000m),
            minimumSweepAmount: Money.Bdt(5000m));

        // Inside band
        var (dir1, amt1) = rule.EvaluateSweep(50000m);
        dir1.Should().BeNull();
        amt1.Should().Be(0m);

        // Surplus is only 2,000 BDT (less than min sweep 5,000 BDT)
        var (dir2, amt2) = rule.EvaluateSweep(102000m);
        dir2.Should().BeNull();
        amt2.Should().Be(0m);
    }
}
