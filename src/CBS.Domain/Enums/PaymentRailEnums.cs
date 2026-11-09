namespace CBS.Domain.Enums;

public enum ClearingSessionType
{
    RegularValue = 1, // Cheques < 500,000 BDT
    HighValue = 2     // Cheques >= 500,000 BDT
}

public enum ClearingDirection
{
    Outward = 1, // Deposited at our bank, drawn on another bank
    Inward = 2   // Drawn on our bank, presented by another bank via BB BACH
}

public enum ChequeClearingStatus
{
    Presented = 1,
    Settled = 2,
    Returned = 3,
    TechnicalReject = 4
}

public enum BachReturnReason
{
    None = 0,
    InsufficientFunds = 1,                     // Code 01
    AccountClosedOrDormant = 2,                // Code 02
    PaymentStoppedByDrawer = 3,                // Code 03
    SignatureMismatch = 4,                     // Code 04
    PostDatedOrStaleCheque = 5,                // Code 05 (> 6 months)
    AmountWordsFiguresDiffer = 6,              // Code 06
    AlterationRequiresDrawerSignature = 7      // Code 07
}

public enum BeftnTransactionType
{
    CreditTransfer = 1, // Salary, Vendor Payment, P2P
    DebitInstruction = 2 // Utility bill collection, Loan EMI
}

public enum BeftnStatus
{
    Queued = 1,
    SentToBb = 2,
    Settled = 3,
    Returned = 4
}

public enum RtgsMessageType
{
    Pacs008_CustomerCreditTransfer = 1,
    Pacs009_FinancialInstitutionTransfer = 2,
    Pacs004_PaymentReturn = 3
}

public enum RtgsStatus
{
    Initiated = 1,
    SettledAtBangladeshBank = 2,
    Rejected = 3,
    Returned = 4
}
