using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Crm.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ICrmAndAdcReconciliationService
{
    // 1. Corporate Group & UBO Hierarchy
    Task<CorporateGroupDetailResponse> AddGroupMemberDetailAsync(string groupId, AddGroupMemberDetailRequest request, CancellationToken ct = default);
    Task<CorporateGroupDetailResponse> RegisterBeneficialOwnerAsync(string groupId, RegisterBeneficialOwnerRequest request, CancellationToken ct = default);
    Task<CorporateGroupDetailResponse> GetGroupHierarchyAsync(string groupId, CancellationToken ct = default);
    Task<bool> ValidateGroupExposureLimitAsync(string groupId, ValidateGroupExposureRequest request, CancellationToken ct = default);

    // 2. Customer Transaction Profile (TP)
    Task<CustomerTransactionProfileResponse> CreateOrUpdateTpAsync(CreateTransactionProfileRequest request, CancellationToken ct = default);
    Task<CustomerTransactionProfileResponse> RecordTpTransactionAsync(string cifNumber, RecordTpTransactionRequest request, CancellationToken ct = default);
    Task<CustomerTransactionProfileResponse> GetTpByCifAsync(string cifNumber, CancellationToken ct = default);
    Task<CustomerTransactionProfileResponse> ClearTpBreachAsync(string cifNumber, ClearTpBreachRequest request, CancellationToken ct = default);

    // 3. ADC Central Reconciliation & Rectification
    Task<AdcReconciliationBatchResponse> CreateReconciliationBatchAsync(CreateReconciliationBatchRequest request, CancellationToken ct = default);
    Task<AdcReconciliationBatchResponse> IngestSwitchRecordAsync(string batchId, IngestSwitchRecordRequest request, CancellationToken ct = default);
    Task<RectificationVoucherDto> GenerateRectificationVoucherAsync(string batchId, GenerateRectificationVoucherRequest request, CancellationToken ct = default);
    Task<AdcReconciliationBatchResponse> GetReconciliationBatchAsync(string batchId, CancellationToken ct = default);
    Task<AdcReconciliationBatchResponse> CloseReconciliationBatchAsync(string batchId, string closedByUserId, CancellationToken ct = default);
}
