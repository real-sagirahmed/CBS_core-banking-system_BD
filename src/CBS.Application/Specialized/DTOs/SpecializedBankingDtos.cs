using CBS.Domain.Enums;

namespace CBS.Application.Specialized.DTOs;

// --- AGRICULTURAL CREDIT DTOS ---
public record InitiateAgriLoanRequest(
    string Cif,
    string KrishiCardNumber,
    string FarmerSavingsAccount,
    CropSeasonType CropType,
    string LandPorchaNumber,
    string UnionParishadRef,
    string BranchCode,
    decimal SanctionLimit,
    decimal AnnualInterestRate,
    DateOnly SanctionDate,
    DateOnly HarvestMaturityDate);

public record DisburseAgriLoanRequest(
    string AccountNumber,
    DateOnly DisbursementDate);

public record RepayAgriLoanRequest(
    string AccountNumber,
    decimal Amount);

public record RescheduleAgriLoanRequest(
    string AccountNumber,
    DateOnly NewMaturityDate,
    string DisasterReference,
    DateOnly RescheduleDate);

public record AgriLoanResponse(
    string AccountNumber,
    string Cif,
    string KrishiCardNumber,
    string CropType,
    decimal SanctionLimit,
    decimal OutstandingPrincipal,
    decimal AccruedSimpleInterest,
    decimal AccruedCharges,
    bool IsNonAccrual,
    string Status);

// --- BILL DISCOUNTING DTOS ---
public record DiscountBillRequest(
    string Cif,
    string BillReferenceNumber,
    string DraweeBankRouting,
    string DraweeBankName,
    string BranchCode,
    decimal BillFaceValue,
    decimal DiscountRatePercent,
    int TenorDays,
    DateOnly PurchaseDate);

public record AmortizeBillDiscountRequest(
    string AccountNumber,
    int ElapsedDays);

public record EarlyRetireBillRequest(
    string AccountNumber);

public record LiquidateBillRequest(
    string AccountNumber,
    decimal RealizedAmount);

public record BillDiscountingResponse(
    string AccountNumber,
    string Cif,
    string BillReferenceNumber,
    string DraweeBankName,
    decimal BillFaceValue,
    decimal UpfrontDiscountAmount,
    decimal NetDisbursedAmount,
    decimal UnearnedDiscountBalance,
    decimal RecognizedPnLInterest,
    string Status);

// --- DEPOSIT MULTIPLICATION DTOS ---
public record OpenMultiplicationAccountRequest(
    string Cif,
    string BranchCode,
    decimal InitialDeposit,
    int MultiplierFactor,
    decimal AnnualInterestRate,
    DateOnly OpenDate);

public record EncashMultiplicationAccountRequest(
    string AccountNumber,
    DateOnly EncashmentDate,
    decimal PrematurePenaltyRate = 0m);

public record MultiplicationAccountResponse(
    string AccountNumber,
    string Cif,
    string BranchCode,
    decimal InitialDeposit,
    int MultiplierFactor,
    int CalculatedTenorMonths,
    decimal MaturityAmount,
    DateOnly OpenDate,
    DateOnly MaturityDate,
    bool IsClosed);

// --- INTER-BRANCH REMITTANCE TEST KEY DTOS ---
public record IssueInterBranchInstrumentRequest(
    string InstrumentNumber,
    InterBranchInstrumentType Type,
    decimal Amount,
    decimal Commission,
    decimal VatAmount,
    string IssuingBranchCode,
    string PayeeBranchCode,
    string BeneficiaryName,
    string RemitterName,
    DateOnly IssueDate,
    string? BeneficiaryAccountNumber = null);

public record VerifyAndPayInterBranchInstrumentRequest(
    string InstrumentNumber,
    string ProvidedTestKey,
    string PayingUserId,
    DateOnly PaymentDate);

public record CancelInterBranchInstrumentRequest(
    string InstrumentNumber,
    string AuthorizingUserId);

public record InterBranchInstrumentResponse(
    string InstrumentNumber,
    string Type,
    decimal Amount,
    decimal Commission,
    decimal VatAmount,
    decimal TotalPayable,
    string IssuingBranchCode,
    string PayeeBranchCode,
    string BeneficiaryName,
    string RemitterName,
    DateOnly IssueDate,
    string CryptographicTestKey,
    bool IsSanctionScreened,
    bool ContraEntryPosted,
    string Status);
