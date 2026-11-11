using CBS.Domain.Enums;

namespace CBS.Domain.Training.ValueObjects;

public sealed record ModuleComplianceCheckResult
{
    public BbrpdModuleId ModuleId { get; init; }
    public string ModuleName { get; init; }
    public string Status { get; init; } // "Compliant", "Deficient"
    public int ControlsAuditedCount { get; init; }
    public int PassingControlsCount { get; init; }
    public string Notes { get; init; }

    #pragma warning disable CS8618
    public ModuleComplianceCheckResult() { }
    #pragma warning restore CS8618

    public ModuleComplianceCheckResult(
        BbrpdModuleId moduleId,
        string moduleName,
        string status,
        int controlsAuditedCount,
        int passingControlsCount,
        string notes)
    {
        ModuleId = moduleId;
        ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        ControlsAuditedCount = controlsAuditedCount;
        PassingControlsCount = passingControlsCount;
        Notes = notes ?? string.Empty;
    }
}
