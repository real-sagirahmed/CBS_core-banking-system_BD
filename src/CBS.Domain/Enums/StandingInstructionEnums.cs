namespace CBS.Domain.Enums;

public enum StandingInstructionType
{
    FixedAmountTransfer = 1,
    MinimumBalanceSweepOut = 2,
    DeficitSweepIn = 3,
    LoanInstallmentRepayment = 4
}

public enum ExecutionFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    SpecificDayOfMonth = 5,
    EventTriggered = 6
}

public enum StandingInstructionStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    Cancelled = 4,
    SuspendedDueToFailures = 5
}

public enum SweepDirection
{
    SweepOut = 1, // Surplus moved to secondary/deposit account
    SweepIn = 2   // Deficit pulled from secondary account to cover operative account
}
