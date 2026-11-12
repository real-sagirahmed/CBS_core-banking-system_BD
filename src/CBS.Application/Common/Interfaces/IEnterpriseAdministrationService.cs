using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Security.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IEnterpriseAdministrationService
{
    Task<UserActiveSessionResponseDto> AuthenticateSessionAsync(LoginWithSessionControlRequest request, CancellationToken ct = default);
    Task<UserActiveSessionResponseDto> TerminateSessionAsync(string sessionId, TerminateSessionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UserActiveSessionResponseDto>> GetActiveSessionsAsync(CancellationToken ct = default);

    Task<AdminChangeRequestResponseDto> SubmitAdminChangeRequestAsync(CreateAdminChangeRequestDto request, CancellationToken ct = default);
    Task<AdminChangeRequestResponseDto> ApproveAdminChangeRequestAsync(string requestId, ApproveAdminChangeRequestDto request, CancellationToken ct = default);
    Task<AdminChangeRequestResponseDto> RejectAdminChangeRequestAsync(string requestId, RejectAdminChangeRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<AdminChangeRequestResponseDto>> GetPendingAdminChangeRequestsAsync(CancellationToken ct = default);

    Task<BankGlobalParametersResponseDto> GetBankParametersAsync(CancellationToken ct = default);
    Task<BankGlobalParametersResponseDto> UpdateBankParametersDirectAsync(UpdateBankGlobalParametersRequest request, CancellationToken ct = default);
    void ValidateFinancialOperationPermission(string? userRole, string operationName);
}
