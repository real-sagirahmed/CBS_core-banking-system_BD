using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class AdvancedStandingInstructionAndMultiSweepTests
{
    private static readonly AccountNumber MasterAcc = AccountNumber.From("0101101000010");
    private static readonly AccountNumber Target1 = AccountNumber.From("0101101000029");
    private static readonly AccountNumber Target2 = AccountNumber.From("0101101000037");

    [Fact]
    public void MultiAccountSweepRule_OneToMany_PercentageSplit_DistributesSurplusAccurately()
    {
        var targets = new List<MultiAccountSweepTarget>
        {
            new(Target1, 60m, 1),
            new(Target2, 40m, 2)
        };

        var rule = new MultiAccountSweepRule(
            "MSWP-001",
            MultiSweepType.OneToMany,
            MasterAcc,
            targets,
            thresholdAmount: Money.Bdt(100000m), // Retain 100k
            minimumSweepAmount: Money.Bdt(10000m),
            strategy: AllocationStrategy.PercentageSplit);

        // Master balance: 200,000 BDT -> Surplus: 100,000 BDT
        // Target 1 gets 60% = 60,000 BDT, Target 2 gets 40% = 40,000 BDT
        var secondaryBalances = new Dictionary<string, decimal>
        {
            [Target1.Value] = 50000m,
            [Target2.Value] = 30000m
        };

        var plans = rule.EvaluateSweep(200000m, secondaryBalances);

        plans.Should().HaveCount(2);
        plans[0].FromAccount.Should().Be(MasterAcc);
        plans[0].ToAccount.Should().Be(Target1);
        plans[0].Amount.Should().Be(60000m);

        plans[1].FromAccount.Should().Be(MasterAcc);
        plans[1].ToAccount.Should().Be(Target2);
        plans[1].Amount.Should().Be(40000m);
    }

    [Fact]
    public void MultiAccountSweepRule_OneToMany_PriorityWaterfall_FillsTargetWithCapFirst()
    {
        var targets = new List<MultiAccountSweepTarget>
        {
            new(Target1, 0m, 1, targetBalanceCap: Money.Bdt(50000m)), // Cap at 50k
            new(Target2, 0m, 2) // Uncapped overflow
        };

        var rule = new MultiAccountSweepRule(
            "MSWP-002",
            MultiSweepType.OneToMany,
            MasterAcc,
            targets,
            thresholdAmount: Money.Bdt(100000m),
            minimumSweepAmount: Money.Bdt(10000m),
            strategy: AllocationStrategy.PriorityWaterfall);

        // Target1 currently has 30,000 BDT -> Headroom is 20,000 BDT
        // Master has 160,000 BDT -> Surplus is 60,000 BDT
        // Target1 takes 20,000 BDT, remainder 40,000 BDT overflows to Target2
        var secondaryBalances = new Dictionary<string, decimal>
        {
            [Target1.Value] = 30000m,
            [Target2.Value] = 10000m
        };

        var plans = rule.EvaluateSweep(160000m, secondaryBalances);

        plans.Should().HaveCount(2);
        plans[0].ToAccount.Should().Be(Target1);
        plans[0].Amount.Should().Be(20000m);

        plans[1].ToAccount.Should().Be(Target2);
        plans[1].Amount.Should().Be(40000m);
    }

    [Fact]
    public void MultiAccountSweepRule_ManyToOne_AggregatesSurplusAboveThreshold()
    {
        var targets = new List<MultiAccountSweepTarget>
        {
            new(Target1, 0m, 1),
            new(Target2, 0m, 2)
        };

        var rule = new MultiAccountSweepRule(
            "MSWP-003",
            MultiSweepType.ManyToOne,
            MasterAcc,
            targets,
            thresholdAmount: Money.Bdt(20000m), // Retain 20k buffer at outlets
            minimumSweepAmount: Money.Bdt(5000m),
            strategy: AllocationStrategy.PercentageSplit);

        // Outlet 1 has 55,000 BDT -> Surplus 35,000 BDT -> sweeps to Master
        // Outlet 2 has 22,000 BDT -> Surplus 2,000 BDT < MinSweep 5k -> skips
        var secondaryBalances = new Dictionary<string, decimal>
        {
            [Target1.Value] = 55000m,
            [Target2.Value] = 22000m
        };

        var plans = rule.EvaluateSweep(10000m, secondaryBalances);

        plans.Should().HaveCount(1);
        plans[0].FromAccount.Should().Be(Target1);
        plans[0].ToAccount.Should().Be(MasterAcc);
        plans[0].Amount.Should().Be(35000m);
    }

    [Fact]
    public void MultiAccountSweepRule_WhenPercentageSumIsNot100_ThrowsInvalidAllocationPercentageSum()
    {
        var targets = new List<MultiAccountSweepTarget>
        {
            new(Target1, 60m, 1),
            new(Target2, 30m, 2) // Sum is 90% != 100%
        };

        var act = () => new MultiAccountSweepRule(
            "MSWP-ERR",
            MultiSweepType.OneToMany,
            MasterAcc,
            targets,
            Money.Bdt(100000m),
            Money.Bdt(5000m),
            AllocationStrategy.PercentageSplit);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*InvalidAllocationPercentageSumInvariant*");
    }

    [Fact]
    public void MultiAccountSweepRule_WhenMasterInTargets_ThrowsMasterTargetIdenticalInvariant()
    {
        var targets = new List<MultiAccountSweepTarget>
        {
            new(MasterAcc, 100m, 1) // Master in targets
        };

        var act = () => new MultiAccountSweepRule(
            "MSWP-ERR2",
            MultiSweepType.OneToMany,
            MasterAcc,
            targets,
            Money.Bdt(100000m),
            Money.Bdt(5000m),
            AllocationStrategy.PercentageSplit);

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*MasterTargetAccountIdenticalInvariant*");
    }

    [Fact]
    public void StandingInstruction_TemporaryHold_SkipsExecutionWithoutIncrementingFailureCount()
    {
        var si = new StandingInstruction(
            "SI-HOLD-01",
            MasterAcc,
            Target1,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Daily,
            Money.Bdt(10000m),
            Money.Bdt(5m),
            new DateOnly(2026, 11, 10));

        si.ApplyTemporaryHold(
            holdUntilDate: new DateOnly(2026, 11, 20),
            holdAmount: 10000m,
            reason: "Client request pending commercial contract review");

        si.IsTemporaryHoldActive.Should().BeTrue();
        si.HoldUntilDate.Should().Be(new DateOnly(2026, 11, 20));
        si.IsUnderHoldAsOf(new DateOnly(2026, 11, 15)).Should().BeTrue();
    }

    [Fact]
    public void StandingInstruction_TemporaryHold_AutoExpiresWhenPastHoldUntilDate()
    {
        var si = new StandingInstruction(
            "SI-HOLD-02",
            MasterAcc,
            Target1,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Daily,
            Money.Bdt(10000m),
            Money.Bdt(5m),
            new DateOnly(2026, 11, 10));

        si.ApplyTemporaryHold(
            holdUntilDate: new DateOnly(2026, 11, 15),
            holdAmount: 10000m,
            reason: "Temporary court hold");

        // Evaluated after hold expiry
        si.IsUnderHoldAsOf(new DateOnly(2026, 11, 16)).Should().BeFalse();
        si.IsTemporaryHoldActive.Should().BeFalse();
    }

    [Fact]
    public void StandingInstruction_ModifyInstruction_UpdatesParametersSuccessfully()
    {
        var si = new StandingInstruction(
            "SI-MOD-01",
            MasterAcc,
            Target1,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Monthly,
            Money.Bdt(5000m),
            Money.Bdt(10m),
            new DateOnly(2026, 11, 1));

        si.ModifyInstruction(
            newTransferAmount: Money.Bdt(12000m),
            newFrequency: ExecutionFrequency.Weekly,
            newExecutionFee: Money.Bdt(15m),
            newTargetAccount: Target2,
            modifierUserId: "OFFICER_01");

        si.TransferAmount.Should().Be(Money.Bdt(12000m));
        si.Frequency.Should().Be(ExecutionFrequency.Weekly);
        si.ExecutionFee.Should().Be(Money.Bdt(15m));
        si.TargetAccountNumber.Should().Be(Target2);
    }

    [Fact]
    public void StandingInstruction_ReleaseTemporaryHold_RestoresActiveExecution()
    {
        var si = new StandingInstruction(
            "SI-HOLD-03",
            MasterAcc,
            Target1,
            StandingInstructionType.FixedAmountTransfer,
            ExecutionFrequency.Daily,
            Money.Bdt(5000m),
            Money.Bdt(5m),
            new DateOnly(2026, 11, 10));

        si.ApplyTemporaryHold(new DateOnly(2026, 11, 30), 5000m, "Temporary freeze");
        si.IsTemporaryHoldActive.Should().BeTrue();

        si.ReleaseTemporaryHold("BRANCH_MANAGER_01");
        si.IsTemporaryHoldActive.Should().BeFalse();
        si.HoldUntilDate.Should().BeNull();
        si.HoldReason.Should().BeNull();
    }
}
