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

public enum DepositProductType
{
    CurrentAccount = 1,
    SavingsAccount = 2,
    SpecialNoticeDeposit = 3,
    FixedDepositReceipt = 4,
    MonthlySavingsScheme = 5
}

public enum AccountOperatingStatus
{
    Active = 1,
    Dormant = 2,
    BlockedDebit = 3,
    BlockedCredit = 4,
    Frozen = 5,
    Unclaimed10YearTransferred = 6,
    Closed = 7,
    Inoperative = 8
}

public enum DayStatus
{
    Open = 1,
    EodBatchRunning = 2,
    Closed = 3
}
