// ARCHITECTURAL INTENT: Aggregate Root managing BACPS Cheque Dishonour Return Advice (Return Memo),
// statutory return fees + 15% VAT, and the Negotiable Instruments Act 1881 Section 138 legal notice lifecycle
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1 (Sections 1.5.2, 1.5.3), BACPS Operating Rules,
// and The Negotiable Instruments Act 1881 (Act No. XXVI of 1881).

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Clearing;

public sealed class ChequeDishonourRecord : AggregateRoot<Guid>
{
    // ARCHITECTURAL INTENT: Return Memo acts as prima facie statutory evidence under NI Act Sec 138.
    // Once issued, memo reference, dishonour reason, and instrument details are immutable.
    public string ChequeNumber { get; private set; } = null!;
    public string DrawerAccountNumber { get; private set; } = null!;
    public string DrawerCif { get; private set; } = null!;
    public string PresenterName { get; private set; } = null!;
    public string PresenterBankCode { get; private set; } = null!;
    public string PresenterBranchCode { get; private set; } = null!;
    public Money ChequeAmount { get; private set; } = null!;
    public ClearingValueType ValueType { get; private set; }
    public DateOnly DishonourDate { get; private set; }
    public BacpsReturnReasonCode ReturnReasonCode { get; private set; }
    public string ReturnReasonDescription { get; private set; } = null!;
    public string MemoReferenceNumber { get; private set; } = null!;
    public Money DishonourFee { get; private set; } = null!;
    public Money VatAmount { get; private set; } = null!;
    public Money TotalChargeDebited { get; private set; } = null!;
    public DishonourMemoStatus Status { get; private set; }
    public string IssuedByUserId { get; private set; } = null!;
    public string? AuthorizedByCheckerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // --- NI ACT SECTION 138 LEGAL NOTICE LIFECYCLE ---
    public NiActNoticeStatus NoticeStatus { get; private set; }
    public string? LegalNoticeReference { get; private set; }
    public DateOnly? NoticeDispatchedDate { get; private set; }
    public DateOnly? NoticeServedDate { get; private set; }
    public DateOnly? CureDeadlineDate { get; private set; }
    public bool IsSection138SuitEligible { get; private set; }
    public string? SuitCaseNumber { get; private set; }
    public DateOnly? SuitFilingDate { get; private set; }
    public string? AdvocateName { get; private set; }

    #pragma warning disable CS8618
    private ChequeDishonourRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public ChequeDishonourRecord(
        Guid id,
        string chequeNumber,
        string drawerAccountNumber,
        string drawerCif,
        string presenterName,
        string presenterBankCode,
        string presenterBranchCode,
        Money chequeAmount,
        DateOnly dishonourDate,
        BacpsReturnReasonCode returnReasonCode,
        string issuedByUserId,
        string? authorizedByCheckerUserId = null,
        decimal baseFeeAmount = 200m)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(chequeNumber))
            throw new ArgumentException("Cheque number is required.", nameof(chequeNumber));
        if (string.IsNullOrWhiteSpace(drawerAccountNumber))
            throw new ArgumentException("Drawer account number is required.", nameof(drawerAccountNumber));
        if (string.IsNullOrWhiteSpace(drawerCif))
            throw new ArgumentException("Drawer CIF is required.", nameof(drawerCif));
        if (string.IsNullOrWhiteSpace(presenterName))
            throw new ArgumentException("Presenter name is required.", nameof(presenterName));
        ArgumentNullException.ThrowIfNull(chequeAmount, nameof(chequeAmount));
        if (chequeAmount.Amount <= 0)
            throw new ArgumentException("Cheque amount must be greater than zero.", nameof(chequeAmount));
        if (string.IsNullOrWhiteSpace(issuedByUserId))
            throw new ArgumentException("Issued by user ID is required.", nameof(issuedByUserId));

        // High Value Clearing Threshold: >= 500,000 BDT requires dual-control authorization (BRPD 1.5.3-vi)
        ValueType = chequeAmount.Amount >= 500_000m ? ClearingValueType.HighValue : ClearingValueType.RegularValue;
        if (ValueType == ClearingValueType.HighValue)
        {
            if (string.IsNullOrWhiteSpace(authorizedByCheckerUserId))
            {
                throw new InvariantViolationException(
                    "HighValueClearingDualControlInvariant",
                    $"Cheque amount of {chequeAmount.Amount} BDT is High Value (>= 500,000 BDT) and strictly mandates checker authorization before issuing dishonour return memo.",
                    "BRPD Circular No. 12 (13 May 2024) Section 1.5.3-vi"
                );
            }
            if (string.Equals(issuedByUserId.Trim(), authorizedByCheckerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvariantViolationException(
                    "MakerCheckerSeparationInvariant",
                    "Maker and Checker user IDs must be strictly distinct for high-value cheque clearing dishonour.",
                    "BRPD Circular No. 12 (13 May 2024) Section 1.5.3-vi"
                );
            }
        }

        ChequeNumber = chequeNumber.Trim();
        DrawerAccountNumber = drawerAccountNumber.Trim();
        DrawerCif = drawerCif.Trim();
        PresenterName = presenterName.Trim();
        PresenterBankCode = presenterBankCode.Trim();
        PresenterBranchCode = presenterBranchCode.Trim();
        ChequeAmount = chequeAmount;
        DishonourDate = dishonourDate;
        ReturnReasonCode = returnReasonCode;
        ReturnReasonDescription = GetDescriptionForCode(returnReasonCode);
        IssuedByUserId = issuedByUserId.Trim();
        AuthorizedByCheckerUserId = authorizedByCheckerUserId?.Trim();
        CreatedAtUtc = DateTime.UtcNow;

        MemoReferenceNumber = $"RET-MEMO-{dishonourDate:yyyyMMdd}-{chequeNumber.Trim()}";

        // Regulatory Fee: Base Fee + 15% VAT
        var fee = Math.Round(baseFeeAmount, 2);
        var vat = Math.Round(fee * 0.15m, 2);
        DishonourFee = new Money(fee, chequeAmount.Currency);
        VatAmount = new Money(vat, chequeAmount.Currency);
        TotalChargeDebited = new Money(fee + vat, chequeAmount.Currency);

        Status = DishonourMemoStatus.Issued;
        NoticeStatus = NiActNoticeStatus.NotIssued;
    }

    public void ServeSection138Notice(
        string legalNoticeReference,
        DateOnly noticeDispatchedDate,
        DateOnly noticeServedDate,
        string advocateName)
    {
        if (string.IsNullOrWhiteSpace(legalNoticeReference))
            throw new ArgumentException("Legal notice reference is required.", nameof(legalNoticeReference));
        if (string.IsNullOrWhiteSpace(advocateName))
            throw new ArgumentException("Advocate name is required.", nameof(advocateName));

        // Invariant: Statutory Notice must be dispatched within 30 days of receiving Return Memo (NI Act Sec 138 Clause b)
        var maxDispatchDate = DishonourDate.AddDays(30);
        if (noticeDispatchedDate > maxDispatchDate)
        {
            throw new InvariantViolationException(
                "Section138NoticeDispatchWindowInvariant",
                $"Section 138 legal notice must be dispatched within 30 days of dishonour memo date ({DishonourDate:yyyy-MM-dd}). Dispatched: {noticeDispatchedDate:yyyy-MM-dd}.",
                "The Negotiable Instruments Act 1881 Section 138(b)"
            );
        }

        if (noticeServedDate < noticeDispatchedDate)
        {
            throw new ArgumentException("Notice served date cannot be prior to notice dispatched date.", nameof(noticeServedDate));
        }

        LegalNoticeReference = legalNoticeReference.Trim();
        NoticeDispatchedDate = noticeDispatchedDate;
        NoticeServedDate = noticeServedDate;
        AdvocateName = advocateName.Trim();

        // Drawer has 30 days from date of receipt to make payment (NI Act Sec 138 Clause c)
        CureDeadlineDate = noticeServedDate.AddDays(30);
        NoticeStatus = NiActNoticeStatus.Delivered;
        Status = DishonourMemoStatus.NoticeServed;
    }

    public void EvaluateNoticeExpiration(DateOnly currentDate)
    {
        if (NoticeStatus == NiActNoticeStatus.Delivered && CureDeadlineDate.HasValue)
        {
            if (currentDate > CureDeadlineDate.Value)
            {
                NoticeStatus = NiActNoticeStatus.ThirtyDayWindowExpired;
                IsSection138SuitEligible = true;
            }
        }
    }

    public void RecordCurePayment(string paymentReference, string officerUserId)
    {
        if (Status == DishonourMemoStatus.Cured)
            throw new InvalidOperationException("Dishonour is already cured.");
        if (string.IsNullOrWhiteSpace(paymentReference))
            throw new ArgumentException("Payment reference is required.", nameof(paymentReference));

        Status = DishonourMemoStatus.Cured;
        IsSection138SuitEligible = false;
    }

    public void FileSection138Lawsuit(string suitCaseNumber, DateOnly suitFilingDate, string courtName)
    {
        if (Status == DishonourMemoStatus.Cured)
        {
            throw new InvariantViolationException(
                "Section138CuredInstrumentSuitProhibitedInvariant",
                "Cannot file Section 138 suit for a cured instrument.",
                "The Negotiable Instruments Act 1881 Section 138"
            );
        }
        if (string.IsNullOrWhiteSpace(suitCaseNumber))
            throw new ArgumentException("Suit case number is required.", nameof(suitCaseNumber));

        if (!IsSection138SuitEligible && (CureDeadlineDate.HasValue && suitFilingDate <= CureDeadlineDate.Value))
        {
            throw new InvariantViolationException(
                "Section138PrematureSuitFilingInvariant",
                $"Cannot file Section 138 lawsuit before the 30-day statutory cure window has elapsed ({CureDeadlineDate:yyyy-MM-dd}).",
                "The Negotiable Instruments Act 1881 Section 138(c)"
            );
        }

        SuitCaseNumber = suitCaseNumber.Trim();
        SuitFilingDate = suitFilingDate;
        NoticeStatus = NiActNoticeStatus.SuitInstituted;
        Status = DishonourMemoStatus.LegalSuitFiled;
    }

    private static string GetDescriptionForCode(BacpsReturnReasonCode code) => code switch
    {
        BacpsReturnReasonCode.RR01_InsufficientFunds => "Drawer's fund is insufficient to honor the cheque.",
        BacpsReturnReasonCode.RR02_SignatureDiffers => "Drawer's signature differs from the specimen lodged with the bank.",
        BacpsReturnReasonCode.RR03_PostDatedCheque => "Cheque presented prior to the date written on the instrument.",
        BacpsReturnReasonCode.RR04_StaleCheque => "Cheque is stale; presented beyond 6 months from the date of issuance.",
        BacpsReturnReasonCode.RR05_PaymentStoppedByDrawer => "Drawer has formally issued a stop-payment instruction.",
        BacpsReturnReasonCode.RR06_WordsAndFiguresDiffer => "The amount stated in words and figures differs.",
        BacpsReturnReasonCode.RR07_AccountBlockedOrFrozen => "Drawer account is restricted, blocked, or frozen by regulatory/court order.",
        BacpsReturnReasonCode.RR08_ChequeMutilatedOrTorn => "Instrument is mutilated, torn, or barcode is illegible.",
        BacpsReturnReasonCode.RR09_IrregularEndorsement => "Endorsement on the instrument is incomplete or irregular.",
        BacpsReturnReasonCode.RR10_AlterationRequiresDrawerSignature => "Alteration on cheque requires drawer's full authentication signature.",
        BacpsReturnReasonCode.RR11_AccountClosed => "Drawer account has been closed.",
        BacpsReturnReasonCode.RR12_PresentedBeyondValidity => "Presented beyond clearing session or validity window.",
        _ => "Cheque returned unpaid as per clearinghouse regulations."
    };
}
