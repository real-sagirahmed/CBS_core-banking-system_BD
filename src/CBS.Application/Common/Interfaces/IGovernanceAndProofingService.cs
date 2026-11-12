using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CBS.Application.Governance.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IGovernanceAndProofingService
{
    // 1. Continuous Transaction Anomaly Detection
    Task<TransactionAnomalyResponse?> ScreenAndDetectAnomalyAsync(DetectAnomalyRequest request, CancellationToken ct = default);
    Task<TransactionAnomalyResponse> ResolveAnomalyAsync(string anomalyId, ResolveAnomalyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionAnomalyResponse>> GetPendingAnomaliesAsync(CancellationToken ct = default);

    // 2. Break-Glass Emergency Privileged Sessions
    Task<BreakGlassSessionResponse> RequestBreakGlassSessionAsync(RequestBreakGlassSessionRequest request, CancellationToken ct = default);
    Task<BreakGlassSessionResponse> ApproveBreakGlassSessionAsync(string sessionId, ApproveBreakGlassSessionRequest request, CancellationToken ct = default);
    Task<BreakGlassSessionResponse> RevokeBreakGlassSessionAsync(string sessionId, RevokeBreakGlassSessionRequest request, CancellationToken ct = default);
    Task<BreakGlassSessionResponse> GetBreakGlassSessionAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ValidateActiveBreakGlassSessionAsync(string sessionId, CancellationToken ct = default);

    // 3. Consolidated Daily EOD Balancing & Proofing
    Task<DailyEodBalancingSheetResponse> GenerateDailyBalancingSheetAsync(GenerateDailyBalancingSheetRequest request, CancellationToken ct = default);
    Task<DailyEodBalancingSheetResponse> ProofDailyBooksAsync(string sheetId, ProofDailyBooksRequest request, CancellationToken ct = default);
    Task<DailyEodBalancingSheetResponse> GetDailyBalancingSheetAsync(string sheetId, CancellationToken ct = default);
}
