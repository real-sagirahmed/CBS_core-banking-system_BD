namespace CBS.Domain.Enums;

// ARCHITECTURAL INTENT: Enumerations for Customer Account Statements, Balance Confirmation,
// NBR Section 102/53F Tax Deduction Withholding Certificates, and SWIFT MT940/MT942 messages
// per Bangladesh Bank BRPD Circular No. 12/2024 Modules 3.1.17, 3.1.19, and 3.1.23.

public enum AccountStatementType
{
    AdHocDemand = 1,        // Customer/Teller initiated on-demand statement (BRPD 3.1.17-i)
    PeriodicScheduled = 2,  // Bulk half-yearly/yearly automated cycle, strictly free of fee (BRPD 3.1.17-ii)
    DuplicateCopy = 3       // Duplicate statement copy, subject to statutory fee (BRPD 3.1.23-i)
}

public enum SwiftStatementType
{
    MT940_CustomerStatement = 1,      // End-of-day / ad-hoc customer statement message
    MT942_InterimTransactionReport = 2 // Intra-day transaction activity report
}

public enum CertificateType
{
    BalanceConfirmation = 1, // On-demand balance confirmation certificate (BRPD 3.1.17-iii)
    TaxDeductionAit = 2      // NBR Section 102/53F annual withholding tax certificate (BRPD 3.1.19-i, ii)
}
