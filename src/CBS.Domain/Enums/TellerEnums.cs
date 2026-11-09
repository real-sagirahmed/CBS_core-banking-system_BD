namespace CBS.Domain.Enums;

public enum CashMovementType
{
    VaultToTeller = 1,
    TellerToVault = 2,
    CustomerCashDeposit = 3,
    CustomerCashWithdrawal = 4
}

public enum TellerDrawerStatus
{
    Closed = 1,
    Active = 2,
    Balanced = 3,
    DiscrepancySuspense = 4
}

public enum RemittanceInstrumentType
{
    PayOrder = 1,
    DemandDraft = 2,
    TelegraphicTransfer = 3
}

public enum RemittanceInstrumentStatus
{
    Issued = 1,
    Liquidated = 2,
    Stopped = 3,
    Cancelled = 4,
    DuplicateIssued = 5
}
