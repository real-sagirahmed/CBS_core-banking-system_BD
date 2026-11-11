using CBS.Domain.Enums;
using CBS.Domain.Training;

namespace CBS.Application.Training;

public interface ITrainingAndAwarenessRepository
{
    // Simulation Sandbox
    Task<TrainingSimulationSession?> GetSimulationSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TrainingSimulationSession>> GetSimulationSessionsByUserIdAsync(string userId, CancellationToken ct = default);
    Task SaveSimulationSessionAsync(TrainingSimulationSession session, CancellationToken ct = default);

    // Employee Certifications
    Task<EmployeeTrainingCertification?> GetCertificationByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeTrainingCertification>> GetCertificationsByEmployeeIdAsync(string employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeTrainingCertification>> GetLapsedCertificationsAsync(DateTime asOfUtc, CancellationToken ct = default);
    Task SaveCertificationAsync(EmployeeTrainingCertification certification, CancellationToken ct = default);

    // Customer Security Broadcasts
    Task<CustomerSecurityBroadcast?> GetBroadcastByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSecurityBroadcast>> GetActiveBroadcastsAsync(DateOnly asOfDate, CancellationToken ct = default);
    Task SaveBroadcastAsync(CustomerSecurityBroadcast broadcast, CancellationToken ct = default);

    // In-App User Manuals & SOPs
    Task<IReadOnlyList<SystemUserManualTopic>> GetUserManualTopicsByModuleAsync(BbrpdModuleId moduleId, CancellationToken ct = default);
    Task<IReadOnlyList<SystemUserManualTopic>> SearchUserManualTopicsAsync(string query, CancellationToken ct = default);
    Task SaveUserManualTopicAsync(SystemUserManualTopic topic, CancellationToken ct = default);
    Task SeedUserManualTopicsIfEmptyAsync(CancellationToken ct = default);
}
