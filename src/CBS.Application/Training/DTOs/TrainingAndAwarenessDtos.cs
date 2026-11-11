using CBS.Domain.Enums;

namespace CBS.Application.Training.DTOs;

public record StartSimulationRequestDto(
    string TraineeUserId,
    string TraineeName,
    string BranchCode,
    SimulationScenarioType ScenarioType);

public record RecordSimulationStepRequestDto(
    int StepNumber,
    string ActionName,
    bool IsCorrect,
    string Feedback);

public record CompleteSimulationRequestDto(
    decimal FinalScore);

public record SimulationSessionDto(
    Guid Id,
    string TraineeUserId,
    string TraineeName,
    string BranchCode,
    SimulationScenarioType ScenarioType,
    DateTime StartTimeUtc,
    DateTime? EndTimeUtc,
    decimal ScorePercentage,
    SimulationSessionStatus Status,
    int CurrentStepNumber,
    int TotalSteps,
    string CompletedStepsJson,
    string SimulationNotes);

public record GrantCertificationRequestDto(
    string EmployeeId,
    string EmployeeName,
    string BranchCode,
    string UserGroup,
    TrainingCurriculumType CurriculumType,
    decimal ScorePercentage,
    string CertificationReference,
    string CertifiedByOfficerId,
    DateTime TrainingDate);

public record EmployeeCertificationDto(
    Guid Id,
    string EmployeeId,
    string EmployeeName,
    string BranchCode,
    string UserGroup,
    TrainingCurriculumType CurriculumType,
    DateTime TrainingDate,
    decimal ScorePercentage,
    decimal PassingScorePercentage,
    bool IsCertified,
    string CertificationReference,
    DateTime CertifiedAtUtc,
    DateTime ExpiresAtUtc,
    string CertifiedByOfficerId,
    CertificationStatus Status);

public record CreateBroadcastRequestDto(
    string Title,
    string MessageEn,
    string MessageBn,
    string Category,
    SecurityBroadcastChannel Channel,
    DateOnly EffectiveStartDate,
    DateOnly EffectiveEndDate,
    string PublishedByUserId);

public record CustomerBroadcastDto(
    Guid Id,
    string Title,
    string MessageEn,
    string MessageBn,
    string Category,
    SecurityBroadcastChannel Channel,
    bool IsActive,
    DateOnly EffectiveStartDate,
    DateOnly EffectiveEndDate,
    long ImpressionCount,
    string PublishedByUserId);

public record UserManualTopicDto(
    string TopicId,
    BbrpdModuleId ModuleId,
    string ModuleTitle,
    string TopicTitle,
    string Summary,
    string StepByStepGuideMarkdown,
    string RegulatoryClauses,
    string InvariantsEnforced);

public record UserManualSearchResultDto(
    string TopicId,
    BbrpdModuleId ModuleId,
    string ModuleTitle,
    string TopicTitle,
    string Summary);

public record ModuleComplianceCheckDto(
    BbrpdModuleId ModuleId,
    string ModuleName,
    string Status,
    int ControlsAuditedCount,
    int PassingControlsCount,
    string Notes);

public record MasterComplianceReportDto(
    string CertificateNumber,
    DateOnly IssueDate,
    DateOnly ComplianceDeadline,
    bool IsFullyCompliant,
    decimal OverallScorePercentage,
    IReadOnlyList<ModuleComplianceCheckDto> ModuleResults,
    string CryptographicSignature);
