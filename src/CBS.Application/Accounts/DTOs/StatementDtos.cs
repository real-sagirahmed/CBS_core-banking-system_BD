using CBS.Domain.Enums;

namespace CBS.Application.Accounts.DTOs;

public record GenerateStatementRequest(
    string AccountNumber,
    DateOnly FromDate,
    DateOnly ToDate,
    AccountStatementType StatementType = AccountStatementType.AdHocDemand,
    bool WaiveFee = false,
    string RequestedByUserId = "TELLER01");

public record StatementTransactionLineDto(
    Guid TransactionId,
    DateOnly ValueDate,
    DateTime BookingDateUtc,
    string Narration,
    string ReferenceNumber,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance);

public record AccountStatementDto(
    string AccountNumber,
    string CustomerName,
    string CifNumber,
    string ProductType,
    string Currency,
    DateOnly FromDate,
    DateOnly ToDate,
    string StatementType,
    decimal OpeningBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal ClosingBalance,
    decimal FeeDeducted,
    decimal VatDeducted,
    List<StatementTransactionLineDto> Transactions,
    DateTime GeneratedAtUtc);

public record GenerateBalanceCertificateRequest(
    string AccountNumber,
    DateOnly AsOfDate,
    string RequestedByUserId = "TELLER01",
    bool WaiveFee = false);

public record BalanceConfirmationCertificateDto(
    string CertificateNumber,
    string AccountNumber,
    string CustomerName,
    string CifNumber,
    string ProductType,
    string Currency,
    DateOnly AsOfDate,
    decimal LedgerBalance,
    decimal TotalActiveHoldAmount,
    decimal AvailableBalance,
    decimal AccruedInterestProvision,
    decimal FeeDeducted,
    decimal VatDeducted,
    string CertificationText,
    DateTime IssuedAtUtc);

public record GenerateTaxCertificateRequest(
    string AccountNumber,
    int FiscalYearStart,
    string RequestedByUserId = "TELLER01");

public record TaxDeductionCertificateDto(
    string CertificateNumber,
    string AccountNumber,
    string CustomerName,
    string CifNumber,
    string TaxIdentificationNumber,
    int FiscalYearStart,
    int FiscalYearEnd,
    decimal GrossInterestCredited,
    decimal TaxRatePercentage,
    decimal TotalTaxWithheld,
    decimal NetInterestCredited,
    string GovernmentTreasuryChallanRef,
    DateTime IssuedAtUtc);

public record GenerateSwiftStatementRequest(
    string AccountNumber,
    DateOnly FromDate,
    DateOnly ToDate,
    SwiftStatementType StatementType = SwiftStatementType.MT940_CustomerStatement,
    int StatementSequenceNumber = 1);

public record SwiftStatementResponseDto(
    string AccountNumber,
    string StatementType,
    int StatementSequenceNumber,
    string SwiftMessageBlock,
    DateTime GeneratedAtUtc);
