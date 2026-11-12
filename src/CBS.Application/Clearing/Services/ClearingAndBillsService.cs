// ARCHITECTURAL INTENT: Service implementation for BACPS Cheque Dishonour Return Advice,
// NI Act Section 138 Legal Notice tracking, Bills for Collection (OBC/IBC), and Inland Bills Purchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4.

using CBS.Application.Clearing.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Clearing;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Clearing.Services;

public sealed class ClearingAndBillsService : IClearingAndBillsService
{
    private readonly IChequeDishonourRepository _chequeRepo;
    private readonly IBillForCollectionRepository _billRepo;
    private readonly IInlandBillPurchasedRepository _ibpRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ClearingAndBillsService(
        IChequeDishonourRepository chequeRepo,
        IBillForCollectionRepository billRepo,
        IInlandBillPurchasedRepository ibpRepo,
        IUnitOfWork unitOfWork)
    {
        _chequeRepo = chequeRepo;
        _billRepo = billRepo;
        _ibpRepo = ibpRepo;
        _unitOfWork = unitOfWork;
    }

    // --- CHEQUE DISHONOUR RETURN MEMO ---

    public async Task<ChequeDishonourRecordDto> IssueDishonourMemoAsync(
        IssueChequeDishonourMemoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var chequeAmount = new Money(command.ChequeAmount, Currency.FromCode(command.Currency ?? "BDT"));
        var record = new ChequeDishonourRecord(
            id: Guid.NewGuid(),
            chequeNumber: command.ChequeNumber,
            drawerAccountNumber: command.DrawerAccountNumber,
            drawerCif: command.DrawerCif,
            presenterName: command.PresenterName,
            presenterBankCode: command.PresenterBankCode,
            presenterBranchCode: command.PresenterBranchCode,
            chequeAmount: chequeAmount,
            dishonourDate: command.DishonourDate,
            returnReasonCode: command.ReturnReasonCode,
            issuedByUserId: command.IssuedByUserId,
            authorizedByCheckerUserId: command.AuthorizedByCheckerUserId,
            baseFeeAmount: command.BaseFeeAmount
        );

        await _chequeRepo.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<ChequeDishonourRecordDto> ServeSection138NoticeAsync(
        ServeSection138NoticeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var record = await _chequeRepo.GetByIdAsync(command.DishonourRecordId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cheque dishonour record '{command.DishonourRecordId}' not found.");

        record.ServeSection138Notice(
            legalNoticeReference: command.LegalNoticeReference,
            noticeDispatchedDate: command.NoticeDispatchedDate,
            noticeServedDate: command.NoticeServedDate,
            advocateName: command.AdvocateName
        );

        await _chequeRepo.UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<ChequeDishonourRecordDto> RecordCurePaymentAsync(
        RecordCurePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var record = await _chequeRepo.GetByIdAsync(command.DishonourRecordId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cheque dishonour record '{command.DishonourRecordId}' not found.");

        record.RecordCurePayment(command.PaymentReference, command.OfficerUserId);

        await _chequeRepo.UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<ChequeDishonourRecordDto> FileSection138SuitAsync(
        FileSection138SuitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var record = await _chequeRepo.GetByIdAsync(command.DishonourRecordId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cheque dishonour record '{command.DishonourRecordId}' not found.");

        record.FileSection138Lawsuit(command.SuitCaseNumber, command.SuitFilingDate, command.CourtName);

        await _chequeRepo.UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(record);
    }

    public async Task<ChequeDishonourRecordDto?> GetDishonourRecordByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await _chequeRepo.GetByIdAsync(id, cancellationToken);
        return record is null ? null : MapToDto(record);
    }

    public async Task<ChequeReturnAdvicePrintDto> GetDishonourAdvicePrintAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var r = await _chequeRepo.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Cheque dishonour record '{id}' not found.");

        return new ChequeReturnAdvicePrintDto(
            MemoReferenceNumber: r.MemoReferenceNumber,
            BankName: "Bangladesh Core Banking Corporation",
            BranchName: "Principal Branch (0001)",
            DateOfAdvice: r.DishonourDate,
            ChequeNumber: r.ChequeNumber,
            ChequeDate: r.DishonourDate,
            ChequeAmount: r.ChequeAmount.Amount,
            Currency: r.ChequeAmount.Currency.Code,
            DrawerAccountNumber: r.DrawerAccountNumber,
            DrawerName: $"Customer CIF: {r.DrawerCif}",
            PresentingBank: r.PresenterBankCode,
            PresentingBranch: r.PresenterBranchCode,
            BacpsCode: r.ReturnReasonCode.ToString(),
            ReturnReason: r.ReturnReasonDescription,
            ReturnCharges: r.DishonourFee.Amount,
            VatCharged: r.VatAmount.Amount,
            StatutoryNotice: "This Return Memo serves as prima facie evidence of dishonour under Section 138 of The Negotiable Instruments Act 1881. Statutory notice must be given within 30 days of receipt."
        );
    }

    // --- BILLS FOR COLLECTION (OBC / IBC) ---

    public async Task<BillForCollectionDto> LodgeBillForCollectionAsync(
        LodgeBillForCollectionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var prefix = command.CollectionType == Domain.Enums.BillCollectionType.OutwardCleanCollection_OBC ? "OBC" : "IBC";
        var billNumber = $"{prefix}-{command.LodgedDate:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var billAmount = new Money(command.BillAmount, Currency.FromCode(command.Currency ?? "BDT"));

        var bill = new BillForCollection(
            id: Guid.NewGuid(),
            billNumber: billNumber,
            collectionType: command.CollectionType,
            customerAccountNumber: command.CustomerAccountNumber,
            customerCif: command.CustomerCif,
            drawerName: command.DrawerName,
            draweeName: command.DraweeName,
            draweeBankName: command.DraweeBankName,
            draweeBranchName: command.DraweeBranchName,
            instrumentNumber: command.InstrumentNumber,
            instrumentDate: command.InstrumentDate,
            billAmount: billAmount,
            lodgedDate: command.LodgedDate,
            lodgedByUserId: command.LodgedByUserId
        );

        await _billRepo.AddAsync(bill, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(bill);
    }

    public async Task<BillForCollectionDto> DispatchBillScheduleAsync(
        DispatchBillScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var bill = await _billRepo.GetByIdAsync(command.BillId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bill for collection '{command.BillId}' not found.");

        bill.DispatchSchedule(command.ScheduleNumber, command.ScheduleDate);

        await _billRepo.UpdateAsync(bill, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(bill);
    }

    public async Task<BillForCollectionDto> RealizeBillForCollectionAsync(
        RealizeBillForCollectionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var bill = await _billRepo.GetByIdAsync(command.BillId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bill for collection '{command.BillId}' not found.");

        bill.RealizeBill(
            commissionAmount: command.CommissionAmount,
            postageAmount: command.PostageAmount,
            realizationDate: command.RealizationDate,
            checkerUserId: command.CheckerUserId
        );

        await _billRepo.UpdateAsync(bill, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(bill);
    }

    public async Task<BillForCollectionDto> DishonourBillForCollectionAsync(
        DishonourBillForCollectionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var bill = await _billRepo.GetByIdAsync(command.BillId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bill for collection '{command.BillId}' not found.");

        bill.MarkDishonoured(command.DishonourReason, command.CheckerUserId);

        await _billRepo.UpdateAsync(bill, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(bill);
    }

    public async Task<BillForCollectionDto?> GetBillByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var bill = await _billRepo.GetByIdAsync(id, cancellationToken);
        return bill is null ? null : MapToDto(bill);
    }

    // --- INLAND BILLS PURCHASED (IBP) ---

    public async Task<InlandBillPurchasedDto> PurchaseInlandBillAsync(
        PurchaseInlandBillCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var refNumber = $"IBP-{command.PurchaseDate:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        var faceValue = new Money(command.FaceValue, Currency.FromCode(command.Currency ?? "BDT"));

        var ibp = new InlandBillPurchased(
            id: Guid.NewGuid(),
            ibpReferenceNumber: refNumber,
            customerAccountNumber: command.CustomerAccountNumber,
            customerCif: command.CustomerCif,
            drawerName: command.DrawerName,
            draweeBankName: command.DraweeBankName,
            instrumentNumber: command.InstrumentNumber,
            faceValue: faceValue,
            marginRatePercent: command.MarginRatePercent,
            discountRateAnnualPercent: command.DiscountRateAnnualPercent,
            purchaseDate: command.PurchaseDate,
            maturityDate: command.MaturityDate,
            purchasedByOfficerUserId: command.PurchasedByOfficerUserId,
            approvedByManagerUserId: command.ApprovedByManagerUserId,
            documentationFeeAmount: command.DocumentationFeeAmount
        );

        await _ibpRepo.AddAsync(ibp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(ibp);
    }

    public async Task<InlandBillPurchasedDto> SettleInlandBillAsync(
        SettleInlandBillCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var ibp = await _ibpRepo.GetByIdAsync(command.IbpId, cancellationToken)
            ?? throw new KeyNotFoundException($"Inland bill purchased '{command.IbpId}' not found.");

        ibp.SettleAtMaturity(command.SettlementDate, command.CheckerUserId);

        await _ibpRepo.UpdateAsync(ibp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(ibp);
    }

    public async Task<InlandBillPurchasedDto> CrystallizeForcedLoanAsync(
        CrystallizeForcedLoanCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var ibp = await _ibpRepo.GetByIdAsync(command.IbpId, cancellationToken)
            ?? throw new KeyNotFoundException($"Inland bill purchased '{command.IbpId}' not found.");

        ibp.CrystallizeForcedLoan(
            forcedLoanAccountNumber: command.ForcedLoanAccountNumber,
            crystallizationDate: command.CrystallizationDate,
            penaltyRatePercent: command.PenaltyRatePercent,
            checkerUserId: command.CheckerUserId
        );

        await _ibpRepo.UpdateAsync(ibp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(ibp);
    }

    public async Task<InlandBillPurchasedDto?> GetIbpByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ibp = await _ibpRepo.GetByIdAsync(id, cancellationToken);
        return ibp is null ? null : MapToDto(ibp);
    }

    // --- MAPPING HELPERS ---

    private static ChequeDishonourRecordDto MapToDto(ChequeDishonourRecord r) =>
        new(
            Id: r.Id,
            ChequeNumber: r.ChequeNumber,
            DrawerAccountNumber: r.DrawerAccountNumber,
            DrawerCif: r.DrawerCif,
            PresenterName: r.PresenterName,
            PresenterBankCode: r.PresenterBankCode,
            PresenterBranchCode: r.PresenterBranchCode,
            ChequeAmount: r.ChequeAmount.Amount,
            Currency: r.ChequeAmount.Currency.Code,
            ValueType: r.ValueType,
            DishonourDate: r.DishonourDate,
            ReturnReasonCode: r.ReturnReasonCode,
            ReturnReasonDescription: r.ReturnReasonDescription,
            MemoReferenceNumber: r.MemoReferenceNumber,
            DishonourFee: r.DishonourFee.Amount,
            VatAmount: r.VatAmount.Amount,
            TotalChargeDebited: r.TotalChargeDebited.Amount,
            Status: r.Status,
            NoticeStatus: r.NoticeStatus,
            LegalNoticeReference: r.LegalNoticeReference,
            NoticeDispatchedDate: r.NoticeDispatchedDate,
            NoticeServedDate: r.NoticeServedDate,
            CureDeadlineDate: r.CureDeadlineDate,
            IsSection138SuitEligible: r.IsSection138SuitEligible,
            SuitCaseNumber: r.SuitCaseNumber,
            SuitFilingDate: r.SuitFilingDate,
            AdvocateName: r.AdvocateName,
            IssuedByUserId: r.IssuedByUserId,
            AuthorizedByCheckerUserId: r.AuthorizedByCheckerUserId
        );

    private static BillForCollectionDto MapToDto(BillForCollection b) =>
        new(
            Id: b.Id,
            BillNumber: b.BillNumber,
            CollectionType: b.CollectionType,
            CustomerAccountNumber: b.CustomerAccountNumber,
            CustomerCif: b.CustomerCif,
            DrawerName: b.DrawerName,
            DraweeName: b.DraweeName,
            DraweeBankName: b.DraweeBankName,
            DraweeBranchName: b.DraweeBranchName,
            InstrumentNumber: b.InstrumentNumber,
            InstrumentDate: b.InstrumentDate,
            BillAmount: b.BillAmount.Amount,
            Currency: b.BillAmount.Currency.Code,
            Status: b.Status,
            ScheduleNumber: b.ScheduleNumber,
            ScheduleDate: b.ScheduleDate,
            LodgedDate: b.LodgedDate,
            RealizationDate: b.RealizationDate,
            CollectionCommission: b.CollectionCommission?.Amount,
            PostageCharges: b.PostageCharges?.Amount,
            VatAmount: b.VatAmount?.Amount,
            NetProceeds: b.NetProceeds?.Amount,
            DishonourReason: b.DishonourReason,
            LodgedByUserId: b.LodgedByUserId,
            VerifiedByCheckerUserId: b.VerifiedByCheckerUserId
        );

    private static InlandBillPurchasedDto MapToDto(InlandBillPurchased i) =>
        new(
            Id: i.Id,
            IbpReferenceNumber: i.IbpReferenceNumber,
            CustomerAccountNumber: i.CustomerAccountNumber,
            CustomerCif: i.CustomerCif,
            DrawerName: i.DrawerName,
            DraweeBankName: i.DraweeBankName,
            InstrumentNumber: i.InstrumentNumber,
            FaceValue: i.FaceValue.Amount,
            Currency: i.FaceValue.Currency.Code,
            MarginRatePercent: i.MarginRatePercent,
            MarginRetained: i.MarginRetained.Amount,
            DiscountRateAnnualPercent: i.DiscountRateAnnualPercent,
            PurchaseDate: i.PurchaseDate,
            MaturityDate: i.MaturityDate,
            TenorDays: i.TenorDays,
            DiscountInterest: i.DiscountInterest.Amount,
            DocumentationFee: i.DocumentationFee.Amount,
            VatAmount: i.VatAmount.Amount,
            DisbursedAmount: i.DisbursedAmount.Amount,
            Status: i.Status,
            ForcedLoanAccountNumber: i.ForcedLoanAccountNumber,
            CrystallizedOverdueAmount: i.CrystallizedOverdueAmount?.Amount,
            CrystallizationDate: i.CrystallizationDate,
            PenaltyInterestRatePercent: i.PenaltyInterestRatePercent,
            PurchasedByOfficerUserId: i.PurchasedByOfficerUserId,
            ApprovedByManagerUserId: i.ApprovedByManagerUserId
        );
}
