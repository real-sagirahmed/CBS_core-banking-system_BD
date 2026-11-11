using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.Training;

// ARCHITECTURAL INTENT: Interactive User Manual & Step-by-Step SOP Engine (BRPD 12.3.1)
// Embeds dynamic, searchable standard operating procedures for all 12 modules of BRPD Circular 12/2024.
public class SystemUserManualTopic : Entity<string>
{
    public string TopicId => Id;
    public BbrpdModuleId ModuleId { get; private set; }
    public string ModuleTitle { get; private set; }
    public string TopicTitle { get; private set; }
    public string Summary { get; private set; }
    public string StepByStepGuideMarkdown { get; private set; }
    public string RegulatoryClauses { get; private set; }
    public string InvariantsEnforced { get; private set; }

    #pragma warning disable CS8618
    protected SystemUserManualTopic() : base(string.Empty) { }
    #pragma warning restore CS8618

    public SystemUserManualTopic(
        string topicId,
        BbrpdModuleId moduleId,
        string moduleTitle,
        string topicTitle,
        string summary,
        string stepByStepGuideMarkdown,
        string regulatoryClauses,
        string invariantsEnforced) : base(topicId.Trim())
    {
        ModuleId = moduleId;
        ModuleTitle = moduleTitle.Trim();
        TopicTitle = topicTitle.Trim();
        Summary = summary.Trim();
        StepByStepGuideMarkdown = stepByStepGuideMarkdown.Trim();
        RegulatoryClauses = regulatoryClauses.Trim();
        InvariantsEnforced = invariantsEnforced.Trim();
    }
}
