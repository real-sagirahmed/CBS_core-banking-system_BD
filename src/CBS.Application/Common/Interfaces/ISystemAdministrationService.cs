using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Security.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ISystemAdministrationService
{
    // 1. Delegation of Authority (DOA) Matrix
    Task<UserDoaProfileResponseDto> ConfigureDoaProfileAsync(ConfigureDoaProfileRequest request, CancellationToken ct = default);
    Task<UserDoaProfileResponseDto?> GetDoaProfileAsync(string userId, CancellationToken ct = default);
    Task<ValidateTransactionDoaResponseDto> ValidateTransactionDoaAsync(ValidateTransactionDoaRequest request, CancellationToken ct = default);

    // 2. Temporary Authority & Absence Delegation
    Task<TemporaryDelegationResponseDto> CreateTemporaryDelegationAsync(CreateTemporaryDelegationRequest request, CancellationToken ct = default);
    Task<TemporaryDelegationResponseDto> ApproveTemporaryDelegationAsync(string delegationId, ApproveTemporaryDelegationRequest request, CancellationToken ct = default);
    Task<TemporaryDelegationResponseDto> RevokeTemporaryDelegationAsync(string delegationId, RevokeTemporaryDelegationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TemporaryDelegationResponseDto>> GetActiveDelegationsAsync(CancellationToken ct = default);

    // 3. Password Lifecycle & History Invariants
    Task<PasswordOperationResponseDto> ChangeUserPasswordAsync(ChangeUserPasswordRequest request, CancellationToken ct = default);
    Task<PasswordOperationResponseDto> AdminResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken ct = default);
}
