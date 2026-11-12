// ARCHITECTURAL INTENT: Service interface for BACPS Cheque Dishonour Return Memo,
// NI Act Section 138 Legal Notice tracking, Bills for Collection (OBC/IBC), and Inland Bills Purchased (IBP)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 1, 2, and 4.

using CBS.Application.Clearing.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IClearingAndBillsService
{
    // --- CHEQUE DISHONOUR RETURN MEMO ---
    Task<ChequeDishonourRecordDto> IssueDishonourMemoAsync(IssueChequeDishonourMemoCommand command, CancellationToken cancellationToken = default);
    Task<ChequeDishonourRecordDto> ServeSection138NoticeAsync(ServeSection138NoticeCommand command, CancellationToken cancellationToken = default);
    Task<ChequeDishonourRecordDto> RecordCurePaymentAsync(RecordCurePaymentCommand command, CancellationToken cancellationToken = default);
    Task<ChequeDishonourRecordDto> FileSection138SuitAsync(FileSection138SuitCommand command, CancellationToken cancellationToken = default);
    Task<ChequeDishonourRecordDto?> GetDishonourRecordByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChequeReturnAdvicePrintDto> GetDishonourAdvicePrintAsync(Guid id, CancellationToken cancellationToken = default);

    // --- BILLS FOR COLLECTION (OBC / IBC) ---
    Task<BillForCollectionDto> LodgeBillForCollectionAsync(LodgeBillForCollectionCommand command, CancellationToken cancellationToken = default);
    Task<BillForCollectionDto> DispatchBillScheduleAsync(DispatchBillScheduleCommand command, CancellationToken cancellationToken = default);
    Task<BillForCollectionDto> RealizeBillForCollectionAsync(RealizeBillForCollectionCommand command, CancellationToken cancellationToken = default);
    Task<BillForCollectionDto> DishonourBillForCollectionAsync(DishonourBillForCollectionCommand command, CancellationToken cancellationToken = default);
    Task<BillForCollectionDto?> GetBillByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // --- INLAND BILLS PURCHASED (IBP) ---
    Task<InlandBillPurchasedDto> PurchaseInlandBillAsync(PurchaseInlandBillCommand command, CancellationToken cancellationToken = default);
    Task<InlandBillPurchasedDto> SettleInlandBillAsync(SettleInlandBillCommand command, CancellationToken cancellationToken = default);
    Task<InlandBillPurchasedDto> CrystallizeForcedLoanAsync(CrystallizeForcedLoanCommand command, CancellationToken cancellationToken = default);
    Task<InlandBillPurchasedDto?> GetIbpByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
