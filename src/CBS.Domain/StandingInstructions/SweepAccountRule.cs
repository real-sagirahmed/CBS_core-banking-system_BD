using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.StandingInstructions;

// ARCHITECTURAL INTENT: Two-way Sweep Account Engine (Sweep-Out surplus / Sweep-In deficit protection)
// linking transactional operative accounts with interest-bearing savings/deposit accounts (BRPD Circular 12/2024 Clause 1.8.1-ii).
public class SweepAccountRule : Entity<string>
{
    public AccountNumber PrimaryAccountNumber { get; private set; }
    public AccountNumber SecondaryAccountNumber { get; private set; }
    public Money UpperThreshold { get; private set; }
    public Money LowerThreshold { get; private set; }
    public Money MinimumSweepAmount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

#pragma warning disable CS8618
    private SweepAccountRule() : base(string.Empty) { }
#pragma warning restore CS8618

    public SweepAccountRule(
        string ruleId,
        AccountNumber primaryAccountNumber,
        AccountNumber secondaryAccountNumber,
        Money upperThreshold,
        Money lowerThreshold,
        Money minimumSweepAmount) : base(ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        ArgumentNullException.ThrowIfNull(primaryAccountNumber, nameof(primaryAccountNumber));
        ArgumentNullException.ThrowIfNull(secondaryAccountNumber, nameof(secondaryAccountNumber));
        ArgumentNullException.ThrowIfNull(upperThreshold, nameof(upperThreshold));
        ArgumentNullException.ThrowIfNull(lowerThreshold, nameof(lowerThreshold));
        ArgumentNullException.ThrowIfNull(minimumSweepAmount, nameof(minimumSweepAmount));

        if (primaryAccountNumber == secondaryAccountNumber)
        {
            throw new InvariantViolationException(
                "SweepAccountsIdenticalInvariant",
                "BRPD Circular 12/2024 (1.8.1-ii)",
                "Primary and secondary accounts for a Sweep Rule cannot be identical.");
        }

        if (upperThreshold <= lowerThreshold)
        {
            throw new InvariantViolationException(
                "SweepThresholdsOrderingInvariant",
                "BRPD Circular 12/2024 (1.8.1-ii)",
                $"Upper threshold ({upperThreshold}) must be strictly greater than lower threshold ({lowerThreshold}).");
        }

        PrimaryAccountNumber = primaryAccountNumber;
        SecondaryAccountNumber = secondaryAccountNumber;
        UpperThreshold = upperThreshold;
        LowerThreshold = lowerThreshold;
        MinimumSweepAmount = minimumSweepAmount;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public (SweepDirection? Direction, decimal AmountToSweep) EvaluateSweep(decimal currentBalance)
    {
        if (!IsActive) return (null, 0m);

        // Sweep-Out: Primary balance exceeds upper threshold -> push surplus to secondary
        if (currentBalance > UpperThreshold.Amount)
        {
            decimal surplus = currentBalance - UpperThreshold.Amount;
            if (surplus >= MinimumSweepAmount.Amount)
            {
                return (SweepDirection.SweepOut, surplus);
            }
        }
        // Sweep-In: Primary balance below lower threshold -> pull deficit from secondary
        else if (currentBalance < LowerThreshold.Amount)
        {
            decimal deficit = LowerThreshold.Amount - currentBalance;
            if (deficit >= MinimumSweepAmount.Amount)
            {
                return (SweepDirection.SweepIn, deficit);
            }
        }

        return (null, 0m);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
