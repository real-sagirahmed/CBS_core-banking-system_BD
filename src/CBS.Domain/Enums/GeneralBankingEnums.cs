namespace CBS.Domain.Enums;

public enum AccountCategory
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5,
    Contingent = 6
}

public enum DebitCredit
{
    Debit = 1,
    Credit = 2
}

public enum VoucherStatus
{
    Draft = 1,
    PendingApproval = 2,
    Posted = 3,
    Reversed = 4
}

public enum VoucherType
{
    Cash = 1,
    Clearing = 2,
    Transfer = 3,
    InterBranch = 4,
    SystemGenerated = 5,
    Reversal = 6
}

public enum MakerCheckerStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4
}
