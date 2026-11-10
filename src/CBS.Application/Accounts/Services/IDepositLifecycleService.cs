using CBS.Application.Accounts.DTOs;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Services;

public interface IDepositLifecycleService
{
    Task<DepositEodSummaryDto> ExecuteBranchDepositEodAsync(
        string branchCode,
        DateOnly businessDate,
        string userId,
        CancellationToken ct = default);

    Task<BatchInterestCapitalizationResponseDto> ExecuteInterestCapitalizationBatchAsync(
        string branchCode,
        DateOnly businessDate,
        string userId,
        CancellationToken ct = default);

    Task RequestDormancyReactivationAsync(
        AccountNumber accountNumber,
        string makerUserId,
        string kycDocRef,
        string remarks,
        CancellationToken ct = default);

    Task<DormancyReactivationResponseDto> ApproveDormancyReactivationAsync(
        AccountNumber accountNumber,
        string branchManagerCheckerUserId,
        string remarks,
        CancellationToken ct = default);

    Task<IReadOnlyList<UnclaimedAccountAgeingDto>> GetUnclaimedAccountsAgeingReportAsync(
        string? branchCode,
        DateOnly asOfDate,
        CancellationToken ct = default);
}
