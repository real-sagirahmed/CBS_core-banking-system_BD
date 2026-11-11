namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enums governing Demand Loans (PAD, LTR, FBP/IBP, Clean Demand Loans)
// and Security Deposit Receipts (SDR) lifecycle conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.5 & 2.1.
public enum DemandLoanType
{
    PaymentAgainstDocuments_PAD = 1,
    LoanAgainstTrustReceipt_LTR = 2,
    BillPurchaseDemandLoan_FBP_IBP = 3,
    CleanDemandLoan = 4,
    ForcedDemandLoan = 5
}

public enum DemandLoanStatus
{
    Sanctioned = 1,
    Disbursed = 2,
    Overdue = 3,
    Restructured = 4,
    Liquidated = 5
}

public enum RepaymentPriorityScheme
{
    Standard = 1,       // BRPD 4.5.4-iii: Charges -> Penal Interest -> Overdue Interest -> Regular Interest -> Principal
    PrincipalFirst = 2  // Restructured / Parameterized: Penal -> Overdue -> Principal -> Regular Interest -> Charges
}

public enum RepaymentSourceType
{
    Cash = 1,
    Clearing = 2,
    Transfer = 3
}

public enum SecurityDepositReceiptStatus
{
    Issued = 1,
    LienMarked = 2,
    ClaimPaid = 3,
    Refunded = 4,
    DuplicateIssued = 5,
    Stopped = 6
}
