namespace CBS.Domain.Training.ValueObjects;

public sealed record MasterComplianceAttestation
{
    public string CertificateNumber { get; init; }
    public DateOnly IssueDate { get; init; }
    public DateOnly ComplianceDeadline { get; init; }
    public bool IsFullyCompliant { get; init; }
    public decimal OverallScorePercentage { get; init; }
    public IReadOnlyList<ModuleComplianceCheckResult> ModuleResults { get; init; }
    public string CryptographicSignature { get; init; }

    #pragma warning disable CS8618
    public MasterComplianceAttestation() { }
    #pragma warning restore CS8618

    public MasterComplianceAttestation(
        string certificateNumber,
        DateOnly issueDate,
        DateOnly complianceDeadline,
        bool isFullyCompliant,
        decimal overallScorePercentage,
        IReadOnlyList<ModuleComplianceCheckResult> moduleResults,
        string cryptographicSignature)
    {
        CertificateNumber = certificateNumber ?? throw new ArgumentNullException(nameof(certificateNumber));
        IssueDate = issueDate;
        ComplianceDeadline = complianceDeadline;
        IsFullyCompliant = isFullyCompliant;
        OverallScorePercentage = overallScorePercentage;
        ModuleResults = moduleResults ?? [];
        CryptographicSignature = cryptographicSignature ?? throw new ArgumentNullException(nameof(cryptographicSignature));
    }
}
