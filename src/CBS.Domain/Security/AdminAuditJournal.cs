using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.Security;

// ARCHITECTURAL INTENT: Immutable Administrative Audit Journal
// Enforces Bangladesh Bank BRPD Circular 12/2024 (10.3.6 & 9.1.1-xvi):
// Preserves immutable journal records for User Profile Management (10.3.6-i),
// Parameter & Configuration Changes (10.3.6-v), and Overrides (10.3.6-iii).
public class AdminAuditJournal : AggregateRoot<Guid>
{
    public JournalCategory Category { get; private set; }
    public string Action { get; private set; }
    public string EntityId { get; private set; }
    public string ActorUserId { get; private set; }
    public string WorkstationIp { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? OldValueJson { get; private set; }
    public string? NewValueJson { get; private set; }
    public string? Remarks { get; private set; }

    #pragma warning disable CS8618
    protected AdminAuditJournal() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public AdminAuditJournal(
        Guid id,
        JournalCategory category,
        string action,
        string entityId,
        string actorUserId,
        string workstationIp,
        DateTime timestampUtc,
        string? oldValueJson = null,
        string? newValueJson = null,
        string? remarks = null) : base(id == Guid.Empty ? Guid.NewGuid() : id)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity ID is required.", nameof(entityId));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user ID is required.", nameof(actorUserId));

        Category = category;
        Action = action.Trim();
        EntityId = entityId.Trim();
        ActorUserId = actorUserId.Trim();
        WorkstationIp = workstationIp?.Trim() ?? "127.0.0.1";
        TimestampUtc = timestampUtc;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        Remarks = remarks?.Trim();
    }
}
