using CBS.Application.Training.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Training;

namespace CBS.Application.Training.Commands;

public class TrainingAndAwarenessCommandHandler
{
    private readonly ITrainingAndAwarenessRepository _repository;

    public TrainingAndAwarenessCommandHandler(ITrainingAndAwarenessRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // 1. Simulation Sandbox
    public async Task<SimulationSessionDto> StartSimulationAsync(
        StartSimulationRequestDto request,
        CancellationToken ct = default)
    {
        var session = TrainingSimulationSession.StartSession(
            request.TraineeUserId,
            request.TraineeName,
            request.BranchCode,
            request.ScenarioType);

        await _repository.SaveSimulationSessionAsync(session, ct);

        return MapToDto(session);
    }

    public async Task<SimulationSessionDto> RecordSimulationStepAsync(
        Guid sessionId,
        RecordSimulationStepRequestDto request,
        CancellationToken ct = default)
    {
        var session = await _repository.GetSimulationSessionByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Simulation session '{sessionId}' not found.");

        session.RecordStepResult(
            request.StepNumber,
            request.ActionName,
            request.IsCorrect,
            request.Feedback);

        await _repository.SaveSimulationSessionAsync(session, ct);

        return MapToDto(session);
    }

    public async Task<SimulationSessionDto> CompleteSimulationAsync(
        Guid sessionId,
        CompleteSimulationRequestDto request,
        CancellationToken ct = default)
    {
        var session = await _repository.GetSimulationSessionByIdAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Simulation session '{sessionId}' not found.");

        session.CompleteSession(request.FinalScore);

        await _repository.SaveSimulationSessionAsync(session, ct);

        return MapToDto(session);
    }

    public async Task<IReadOnlyList<SimulationSessionDto>> GetSimulationsByUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        var sessions = await _repository.GetSimulationSessionsByUserIdAsync(userId, ct);
        return sessions.Select(MapToDto).ToList();
    }

    // 2. Employee Training Certifications
    public async Task<EmployeeCertificationDto> GrantCertificationAsync(
        GrantCertificationRequestDto request,
        CancellationToken ct = default)
    {
        var cert = EmployeeTrainingCertification.GrantCertification(
            request.EmployeeId,
            request.EmployeeName,
            request.BranchCode,
            request.UserGroup,
            request.CurriculumType,
            request.ScorePercentage,
            request.CertificationReference,
            request.CertifiedByOfficerId,
            request.TrainingDate);

        await _repository.SaveCertificationAsync(cert, ct);

        return MapToDto(cert);
    }

    public async Task<IReadOnlyList<EmployeeCertificationDto>> GetCertificationsByEmployeeAsync(
        string employeeId,
        CancellationToken ct = default)
    {
        var certs = await _repository.GetCertificationsByEmployeeIdAsync(employeeId, ct);
        return certs.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<EmployeeCertificationDto>> GetLapsedCertificationsAsync(
        DateTime asOfUtc,
        CancellationToken ct = default)
    {
        var certs = await _repository.GetLapsedCertificationsAsync(asOfUtc, ct);
        return certs.Select(MapToDto).ToList();
    }

    // 3. Customer Security Broadcasts
    public async Task<CustomerBroadcastDto> PublishBroadcastAsync(
        CreateBroadcastRequestDto request,
        CancellationToken ct = default)
    {
        var broadcast = new CustomerSecurityBroadcast(
            request.Title,
            request.MessageEn,
            request.MessageBn,
            request.Category,
            request.Channel,
            request.EffectiveStartDate,
            request.EffectiveEndDate,
            request.PublishedByUserId);

        await _repository.SaveBroadcastAsync(broadcast, ct);

        return MapToDto(broadcast);
    }

    public async Task<IReadOnlyList<CustomerBroadcastDto>> GetActiveBroadcastsAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var broadcasts = await _repository.GetActiveBroadcastsAsync(asOfDate, ct);
        return broadcasts.Select(MapToDto).ToList();
    }

    // 4. In-App User Manuals & SOPs
    public async Task<IReadOnlyList<UserManualTopicDto>> GetUserManualTopicsByModuleAsync(
        BbrpdModuleId moduleId,
        CancellationToken ct = default)
    {
        await _repository.SeedUserManualTopicsIfEmptyAsync(ct);
        var topics = await _repository.GetUserManualTopicsByModuleAsync(moduleId, ct);
        return topics.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<UserManualSearchResultDto>> SearchUserManualTopicsAsync(
        string query,
        CancellationToken ct = default)
    {
        await _repository.SeedUserManualTopicsIfEmptyAsync(ct);
        var topics = await _repository.SearchUserManualTopicsAsync(query, ct);
        return topics.Select(t => new UserManualSearchResultDto(
            t.TopicId,
            t.ModuleId,
            t.ModuleTitle,
            t.TopicTitle,
            t.Summary)).ToList();
    }

    // Helpers
    private static SimulationSessionDto MapToDto(TrainingSimulationSession s) => new(
        s.Id,
        s.TraineeUserId,
        s.TraineeName,
        s.BranchCode,
        s.ScenarioType,
        s.StartTimeUtc,
        s.EndTimeUtc,
        s.ScorePercentage,
        s.Status,
        s.CurrentStepNumber,
        s.TotalSteps,
        s.CompletedStepsJson,
        s.SimulationNotes);

    private static EmployeeCertificationDto MapToDto(EmployeeTrainingCertification c) => new(
        c.Id,
        c.EmployeeId,
        c.EmployeeName,
        c.BranchCode,
        c.UserGroup,
        c.CurriculumType,
        c.TrainingDate,
        c.ScorePercentage,
        c.PassingScorePercentage,
        c.IsCertified,
        c.CertificationReference,
        c.CertifiedAtUtc,
        c.ExpiresAtUtc,
        c.CertifiedByOfficerId,
        c.EvaluateStatus(DateTime.UtcNow));

    private static CustomerBroadcastDto MapToDto(CustomerSecurityBroadcast b) => new(
        b.Id,
        b.Title,
        b.MessageEn,
        b.MessageBn,
        b.Category,
        b.Channel,
        b.IsActive,
        b.EffectiveStartDate,
        b.EffectiveEndDate,
        b.ImpressionCount,
        b.PublishedByUserId);

    private static UserManualTopicDto MapToDto(SystemUserManualTopic t) => new(
        t.TopicId,
        t.ModuleId,
        t.ModuleTitle,
        t.TopicTitle,
        t.Summary,
        t.StepByStepGuideMarkdown,
        t.RegulatoryClauses,
        t.InvariantsEnforced);
}
