namespace CBS.Domain.Enums;

public enum DpsStatus
{
    Active = 1,
    Matured = 2,
    Encashed = 3,
    DefaultSuspended = 4,
    LienMarked = 5,
    Closed = 6
}

public enum DpsInstallmentStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    Waived = 4
}

public enum DpsSettlementMode
{
    LumpSumCreditToCasa = 1,
    MonthlyPensionAnnuity = 2,
    PayOrderIssuance = 3
}

public enum DepositLienStatus
{
    Active = 1,
    Released = 2,
    EnforcedLiquidation = 3
}
