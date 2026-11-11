using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.StandingInstructions;

public record MultiAccountSweepTarget
{
    public AccountNumber TargetAccountNumber { get; init; }
    public decimal AllocationPercentage { get; init; }
    public int PriorityOrder { get; init; }
    public Money? TargetBalanceCap { get; init; }

    public MultiAccountSweepTarget(
        AccountNumber targetAccountNumber,
        decimal allocationPercentage,
        int priorityOrder = 1,
        Money? targetBalanceCap = null)
    {
        TargetAccountNumber = targetAccountNumber ?? throw new ArgumentNullException(nameof(targetAccountNumber));
        if (allocationPercentage < 0m || allocationPercentage > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocationPercentage), "Allocation percentage must be between 0 and 100.");
        }
        AllocationPercentage = allocationPercentage;
        PriorityOrder = priorityOrder;
        TargetBalanceCap = targetBalanceCap;
    }
}

public record SweepLegExecutionPlan(
    AccountNumber FromAccount,
    AccountNumber ToAccount,
    decimal Amount,
    string Narrative);

// ARCHITECTURAL INTENT: Multi-Account Sweep Rule Aggregate (BRPD Circular 12/2024 Clause 1.8.1-i, ii).
// Supports One-to-Many surplus distribution and Many-to-One collection aggregation
// with mathematical percentage split or priority waterfall liquidation.
public class MultiAccountSweepRule : AggregateRoot<string>
{
    public MultiSweepType Type { get; private set; }
    public AccountNumber MasterAccountNumber { get; private set; }
    public List<MultiAccountSweepTarget> Targets { get; private set; } = new();
    public Money ThresholdAmount { get; private set; }
    public Money MinimumSweepAmount { get; private set; }
    public AllocationStrategy Strategy { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private MultiAccountSweepRule() : base(string.Empty) { }
#pragma warning restore CS8618

    public MultiAccountSweepRule(
        string ruleId,
        MultiSweepType type,
        AccountNumber masterAccountNumber,
        IEnumerable<MultiAccountSweepTarget> targets,
        Money thresholdAmount,
        Money minimumSweepAmount,
        AllocationStrategy strategy) : base(ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        ArgumentNullException.ThrowIfNull(masterAccountNumber, nameof(masterAccountNumber));
        ArgumentNullException.ThrowIfNull(thresholdAmount, nameof(thresholdAmount));
        ArgumentNullException.ThrowIfNull(minimumSweepAmount, nameof(minimumSweepAmount));

        var targetList = targets?.ToList() ?? throw new ArgumentNullException(nameof(targets));
        if (targetList.Count == 0)
        {
            throw new InvariantViolationException(
                "EmptySweepTargetsInvariant",
                "BRPD Circular 12/2024 (1.8.1-i)",
                "Multi-account sweep rule requires at least one target account.");
        }

        if (targetList.Any(t => t.TargetAccountNumber == masterAccountNumber))
        {
            throw new InvariantViolationException(
                "MasterTargetAccountIdenticalInvariant",
                "BRPD Circular 12/2024 (1.8.1-i)",
                "Master account cannot be included in the sweep targets list.");
        }

        if (type == MultiSweepType.OneToMany && strategy == AllocationStrategy.PercentageSplit)
        {
            decimal totalPercentage = targetList.Sum(t => t.AllocationPercentage);
            if (Math.Abs(totalPercentage - 100m) > 0.01m)
            {
                throw new InvariantViolationException(
                    "InvalidAllocationPercentageSumInvariant",
                    "BRPD Circular 12/2024 (1.8.1-i)",
                    $"Sum of target allocation percentages must equal 100%. Current sum: {totalPercentage}%.");
            }
        }

        Type = type;
        MasterAccountNumber = masterAccountNumber;
        Targets = targetList;
        ThresholdAmount = thresholdAmount;
        MinimumSweepAmount = minimumSweepAmount;
        Strategy = strategy;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public List<SweepLegExecutionPlan> EvaluateSweep(
        decimal masterBalance,
        IReadOnlyDictionary<string, decimal> secondaryBalances)
    {
        var plans = new List<SweepLegExecutionPlan>();
        if (!IsActive) return plans;

        if (Type == MultiSweepType.OneToMany)
        {
            // One-to-Many: Master has surplus above threshold -> disperse to targets
            if (masterBalance > ThresholdAmount.Amount)
            {
                decimal surplus = masterBalance - ThresholdAmount.Amount;
                if (surplus >= MinimumSweepAmount.Amount)
                {
                    if (Strategy == AllocationStrategy.PercentageSplit)
                    {
                        decimal allocatedSoFar = 0m;
                        for (int i = 0; i < Targets.Count; i++)
                        {
                            var target = Targets[i];
                            decimal legAmount;
                            if (i == Targets.Count - 1)
                            {
                                // Absorb minor rounding discrepancy into the last leg
                                legAmount = surplus - allocatedSoFar;
                            }
                            else
                            {
                                legAmount = Math.Round(surplus * (target.AllocationPercentage / 100m), 2);
                                allocatedSoFar += legAmount;
                            }

                            if (legAmount > 0m)
                            {
                                plans.Add(new SweepLegExecutionPlan(
                                    MasterAccountNumber,
                                    target.TargetAccountNumber,
                                    legAmount,
                                    $"OneToMany Sweep-Out ({target.AllocationPercentage}%) to {target.TargetAccountNumber.Value}"));
                            }
                        }
                    }
                    else if (Strategy == AllocationStrategy.PriorityWaterfall)
                    {
                        decimal remainingSurplus = surplus;
                        foreach (var target in Targets.OrderBy(t => t.PriorityOrder))
                        {
                            if (remainingSurplus <= 0m) break;

                            decimal targetBalance = secondaryBalances.GetValueOrDefault(target.TargetAccountNumber.Value, 0m);
                            decimal legAmount = remainingSurplus;

                            if (target.TargetBalanceCap is not null)
                            {
                                decimal headroom = Math.Max(0m, target.TargetBalanceCap.Amount - targetBalance);
                                legAmount = Math.Min(remainingSurplus, headroom);
                            }

                            if (legAmount > 0m)
                            {
                                plans.Add(new SweepLegExecutionPlan(
                                    MasterAccountNumber,
                                    target.TargetAccountNumber,
                                    legAmount,
                                    $"OneToMany Waterfall Sweep to {target.TargetAccountNumber.Value} (Priority {target.PriorityOrder})"));
                                remainingSurplus -= legAmount;
                            }
                        }
                    }
                }
            }
        }
        else if (Type == MultiSweepType.ManyToOne)
        {
            // Many-to-One: Target/outlet accounts retain buffer, surplus pooled into Master
            foreach (var target in Targets)
            {
                decimal outletBalance = secondaryBalances.GetValueOrDefault(target.TargetAccountNumber.Value, 0m);
                if (outletBalance > ThresholdAmount.Amount)
                {
                    decimal surplus = outletBalance - ThresholdAmount.Amount;
                    if (surplus >= MinimumSweepAmount.Amount)
                    {
                        plans.Add(new SweepLegExecutionPlan(
                            target.TargetAccountNumber,
                            MasterAccountNumber,
                            surplus,
                            $"ManyToOne Outlet Collection Sweep from {target.TargetAccountNumber.Value} to {MasterAccountNumber.Value}"));
                    }
                }
            }
        }

        return plans;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
