// ARCHITECTURAL INTENT: DTOs and commands for BACPS Cheque Dishonour Return Advice,
// NI Act Section 138 Legal Notice tracking, Bills for Collection (OBC/IBC), and Inland Bills Purchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4.

using CBS.Domain.Enums;

namespace CBS.Application.Clearing.DTOs;

// --- CHEQUE DISHONOUR RETURN MEMO ---
public sealed record IssueChequeDishonourMemoCommand(
    string ChequeNumber,
    string DrawerAccountNumber,
    string DrawerCif,
    string PresenterName,
    string PresenterBankCode,
    string PresenterBranchCode,
    decimal ChequeAmount,
    string Currency,
    DateOnly DishonourDate,
    BacpsReturnReasonCode ReturnReasonCode,
    string IssuedByUserId,
    string? AuthorizedByCheckerUserId = null,
    decimal BaseFeeAmount = 200m
);

public sealed record ServeSection138NoticeCommand(
    Guid DishonourRecordId,
    string LegalNoticeReference,
    DateOnly NoticeDispatchedDate,
    DateOnly NoticeServedDate,
    string AdvocateName
);

public sealed record RecordCurePaymentCommand(
    Guid DishonourRecordId,
    string PaymentReference,
    string OfficerUserId
);

public sealed record FileSection138SuitCommand(
    Guid DishonourRecordId,
    string SuitCaseNumber,
    DateOnly SuitFilingDate,
    string CourtName
);

public sealed record ChequeDishonourRecordDto(
    Guid Id,
    string ChequeNumber,
    string DrawerAccountNumber,
    string DrawerCif,
    string PresenterName,
    string PresenterBankCode,
    string PresenterBranchCode,
    decimal ChequeAmount,
    string Currency,
    ClearingValueType ValueType,
    DateOnly DishonourDate,
    BacpsReturnReasonCode ReturnReasonCode,
    string ReturnReasonDescription,
    string MemoReferenceNumber,
    decimal DishonourFee,
    decimal VatAmount,
    decimal TotalChargeDebited,
    DishonourMemoStatus Status,
    NiActNoticeStatus NoticeStatus,
    string? LegalNoticeReference,
    DateOnly? NoticeDispatchedDate,
    DateOnly? NoticeServedDate,
    DateOnly? CureDeadlineDate,
    bool IsSection138SuitEligible,
    string? SuitCaseNumber,
    DateOnly? SuitFilingDate,
    string? AdvocateName,
    string IssuedByUserId,
    string? AuthorizedByCheckerUserId
);

public sealed record ChequeReturnAdvicePrintDto(
    string MemoReferenceNumber,
    string BankName,
    string BranchName,
    DateOnly DateOfAdvice,
    string ChequeNumber,
    DateOnly ChequeDate,
    decimal ChequeAmount,
    string Currency,
    string DrawerAccountNumber,
    string DrawerName,
    string PresentingBank,
    string PresentingBranch,
    string BacpsCode,
    string ReturnReason,
    decimal ReturnCharges,
    decimal VatCharged,
    string StatutoryNotice
);

// --- BILLS FOR COLLECTION (OBC / IBC) ---
public sealed record LodgeBillForCollectionCommand(
    BillCollectionType CollectionType,
    string CustomerAccountNumber,
    string CustomerCif,
    string DrawerName,
    string DraweeName,
    string DraweeBankName,
    string DraweeBranchName,
    string InstrumentNumber,
    DateOnly InstrumentDate,
    decimal BillAmount,
    string Currency,
    DateOnly LodgedDate,
    string LodgedByUserId
);

public sealed record DispatchBillScheduleCommand(
    Guid BillId,
    string ScheduleNumber,
    DateOnly ScheduleDate
);

public sealed record RealizeBillForCollectionCommand(
    Guid BillId,
    decimal CommissionAmount,
    decimal PostageAmount,
    DateOnly RealizationDate,
    string CheckerUserId
);

public sealed record DishonourBillForCollectionCommand(
    Guid BillId,
    string DishonourReason,
    string CheckerUserId
);

public sealed record BillForCollectionDto(
    Guid Id,
    string BillNumber,
    BillCollectionType CollectionType,
    string CustomerAccountNumber,
    string CustomerCif,
    string DrawerName,
    string DraweeName,
    string DraweeBankName,
    string DraweeBranchName,
    string InstrumentNumber,
    DateOnly InstrumentDate,
    decimal BillAmount,
    string Currency,
    BillCollectionStatus Status,
    string? ScheduleNumber,
    DateOnly? ScheduleDate,
    DateOnly LodgedDate,
    DateOnly? RealizationDate,
    decimal? CollectionCommission,
    decimal? PostageCharges,
    decimal? VatAmount,
    decimal? NetProceeds,
    string? DishonourReason,
    string LodgedByUserId,
    string? VerifiedByCheckerUserId
);

// --- INLAND BILLS PURCHASED (IBP) ---
public sealed record PurchaseInlandBillCommand(
    string CustomerAccountNumber,
    string CustomerCif,
    string DrawerName,
    string DraweeBankName,
    string InstrumentNumber,
    decimal FaceValue,
    string Currency,
    decimal MarginRatePercent,
    decimal DiscountRateAnnualPercent,
    DateOnly PurchaseDate,
    DateOnly MaturityDate,
    string PurchasedByOfficerUserId,
    string ApprovedByManagerUserId,
    decimal DocumentationFeeAmount = 500m
);

public sealed record SettleInlandBillCommand(
    Guid IbpId,
    DateOnly SettlementDate,
    string CheckerUserId
);

public sealed record CrystallizeForcedLoanCommand(
    Guid IbpId,
    string ForcedLoanAccountNumber,
    DateOnly CrystallizationDate,
    decimal PenaltyRatePercent,
    string CheckerUserId
);

public sealed record InlandBillPurchasedDto(
    Guid Id,
    string IbpReferenceNumber,
    string CustomerAccountNumber,
    string CustomerCif,
    string DrawerName,
    string DraweeBankName,
    string InstrumentNumber,
    decimal FaceValue,
    string Currency,
    decimal MarginRatePercent,
    decimal MarginRetained,
    decimal DiscountRateAnnualPercent,
    DateOnly PurchaseDate,
    DateOnly MaturityDate,
    int TenorDays,
    decimal DiscountInterest,
    decimal DocumentationFee,
    decimal VatAmount,
    decimal DisbursedAmount,
    InlandBillPurchasedStatus Status,
    string? ForcedLoanAccountNumber,
    decimal? CrystallizedOverdueAmount,
    DateOnly? CrystallizationDate,
    decimal? PenaltyInterestRatePercent,
    string PurchasedByOfficerUserId,
    string ApprovedByManagerUserId
);
