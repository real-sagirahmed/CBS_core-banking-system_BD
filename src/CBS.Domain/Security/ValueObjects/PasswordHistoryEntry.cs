namespace CBS.Domain.Security.ValueObjects;

// ARCHITECTURAL INTENT: Retains historical password hashes
// in strict compliance with BRPD 10.1.3-v (Password history shall be maintained at least four times).
public sealed record PasswordHistoryEntry
{
    public string PasswordHash { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    #pragma warning disable CS8618
    public PasswordHistoryEntry() { }
    #pragma warning restore CS8618

    public PasswordHistoryEntry(string passwordHash, DateTime createdAtUtc)
    {
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }
}
