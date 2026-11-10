namespace CBS.Domain.Enums;

public enum TermDepositMaturityInstruction
{
    AutoRenewPrincipalAndInterest = 1,     // Compounding: Roll principal + net interest into new tenor
    AutoRenewPrincipalOnly = 2,            // Roll principal, sweep net interest to operative account
    CreditToOperativeAccountAndClose = 3   // Pay principal + net interest to operative account and close
}

public enum TermDepositStatus
{
    Active = 1,
    MaturedRenewed = 2,
    ClosedOnMaturity = 3,
    PrematurelyClosed = 4,
    LienMarked = 5
}
