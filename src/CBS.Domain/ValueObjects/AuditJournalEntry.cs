using CBS.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace CBS.Domain.ValueObjects;

/// <summary>
/// Tamper-evident Audit Journal Entry conforming to Bangladesh Bank BRPD Circular 12 (Module 10: Security & Audit Trail).
/// Invariants:
/// 1. Consecutive entries are cryptographically linked using SHA-256 hash chaining (BRPD 10.3.5-vii, 10.3.6).
/// 2. Any retroactive edit or row deletion invalidates the chain integrity check.
/// </summary>
public sealed record AuditJournalEntry
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    public long SequenceNumber { get; }
    public DateTime TimestampUtc { get; }
    public string UserId { get; }
    public UserRole Role { get; }
    public string BranchCode { get; }
    public AuditEventCategory EventCategory { get; }
    public string Action { get; }
    public string TargetEntityName { get; }
    public string TargetEntityId { get; }
    public string? BeforeStateJson { get; }
    public string? AfterStateJson { get; }
    public string TerminalIp { get; }
    public string PreviousEntryHash { get; }
    public string CurrentEntryHash { get; }

    private AuditJournalEntry(
        long sequenceNumber,
        DateTime timestampUtc,
        string userId,
        UserRole role,
        string branchCode,
        AuditEventCategory eventCategory,
        string action,
        string targetEntityName,
        string targetEntityId,
        string? beforeStateJson,
        string? afterStateJson,
        string terminalIp,
        string previousEntryHash,
        string currentEntryHash)
    {
        SequenceNumber = sequenceNumber;
        TimestampUtc = timestampUtc;
        UserId = userId;
        Role = role;
        BranchCode = branchCode;
        EventCategory = eventCategory;
        Action = action;
        TargetEntityName = targetEntityName;
        TargetEntityId = targetEntityId;
        BeforeStateJson = beforeStateJson;
        AfterStateJson = afterStateJson;
        TerminalIp = terminalIp;
        PreviousEntryHash = previousEntryHash;
        CurrentEntryHash = currentEntryHash;
    }

    public static AuditJournalEntry Create(
        long sequenceNumber,
        string userId,
        UserRole role,
        string branchCode,
        AuditEventCategory eventCategory,
        string action,
        string targetEntityName,
        string targetEntityId,
        string? beforeStateJson,
        string? afterStateJson,
        string terminalIp,
        string previousEntryHash)
    {
        if (sequenceNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be greater than zero.");
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(targetEntityName))
            throw new ArgumentException("Target entity name is required.", nameof(targetEntityName));
        if (string.IsNullOrWhiteSpace(targetEntityId))
            throw new ArgumentException("Target entity ID is required.", nameof(targetEntityId));

        var timestamp = DateTime.UtcNow;
        var prevHash = string.IsNullOrWhiteSpace(previousEntryHash) ? GenesisHash : previousEntryHash.Trim();

        var payload = $"{prevHash}|{sequenceNumber}|{timestamp:O}|{userId.Trim()}|{role}|{branchCode.Trim()}|{eventCategory}|{action.Trim()}|{targetEntityName.Trim()}|{targetEntityId.Trim()}|{beforeStateJson}|{afterStateJson}|{terminalIp.Trim()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new AuditJournalEntry(
            sequenceNumber,
            timestamp,
            userId.Trim(),
            role,
            branchCode.Trim(),
            eventCategory,
            action.Trim(),
            targetEntityName.Trim(),
            targetEntityId.Trim(),
            beforeStateJson,
            afterStateJson,
            terminalIp.Trim(),
            prevHash,
            currentHash);
    }

    public bool VerifyHash()
    {
        var payload = $"{PreviousEntryHash}|{SequenceNumber}|{TimestampUtc:O}|{UserId}|{Role}|{BranchCode}|{EventCategory}|{Action}|{TargetEntityName}|{TargetEntityId}|{BeforeStateJson}|{AfterStateJson}|{TerminalIp}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var recomputedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return string.Equals(CurrentEntryHash, recomputedHash, StringComparison.OrdinalIgnoreCase);
    }
}
