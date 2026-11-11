namespace CBS.Domain.Security.ValueObjects;

// ARCHITECTURAL INTENT: Logs login telemetry and surfaces prior attempt details
// in compliance with BRPD 9.1.1-xviii.
public sealed record LoginAttemptLog
{
    public string WorkstationIp { get; init; }
    public DateTime AttemptedAtUtc { get; init; }
    public bool IsSuccessful { get; init; }
    public string? FailureReason { get; init; }

    #pragma warning disable CS8618
    public LoginAttemptLog() { }
    #pragma warning restore CS8618

    public LoginAttemptLog(string workstationIp, DateTime attemptedAtUtc, bool isSuccessful, string? failureReason = null)
    {
        WorkstationIp = workstationIp;
        AttemptedAtUtc = attemptedAtUtc;
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
    }
}
