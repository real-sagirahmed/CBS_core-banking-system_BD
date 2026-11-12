using CBS.Application.TradeFinance.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IMasterExportLcService
{
    Task<MasterExportLcResponseDto> RegisterMasterExportLcAsync(RegisterMasterExportLcRequest request, CancellationToken cancellationToken = default);
    Task<MasterExportLcResponseDto> AuthorizeMasterExportLcAsync(string masterLcNumber, AuthorizeMasterExportLcRequest request, CancellationToken cancellationToken = default);
    Task<MasterExportLcResponseDto> GetMasterExportLcAsync(string masterLcNumber, CancellationToken cancellationToken = default);
    Task<B2bLcIssuedResponseDto> IssueB2bLcUnderMasterAsync(string masterLcNumber, OpenB2bLcUnderMasterRequest request, CancellationToken cancellationToken = default);
    Task<MasterExportLcResponseDto> AmendMasterExportLcAsync(string masterLcNumber, AmendMasterLcRequest request, CancellationToken cancellationToken = default);
    Task<MasterExportLcResponseDto> CloseMasterExportLcAsync(string masterLcNumber, CloseMasterLcRequest request, CancellationToken cancellationToken = default);
    Task<MasterLcHistoryReportDto> GetMasterLcHistoryReportAsync(string masterLcNumber, CancellationToken cancellationToken = default);
}
