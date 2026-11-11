using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Training;

// ARCHITECTURAL INTENT: In compliance with BRPD Circular 12/2024 Clause 12.1.2 & 12.1.4:
// 1. Mandatory compliance certifications require a minimum passing score of 80.0%.
// 2. Certifications have a strict validity period of exactly 365 calendar days (1 year).
// 3. Expired certifications systematically flag the employee as "Lapsed"
//    for regulatory audit and supervisory inspection.
public class EmployeeTrainingCertification : AggregateRoot<Guid>
{
    public string EmployeeId { get; private set; }
    public string EmployeeName { get; private set; }
    public string BranchCode { get; private set; }
    public string UserGroup { get; private set; }
    public TrainingCurriculumType CurriculumType { get; private set; }
    public DateTime TrainingDate { get; private set; }
    public decimal ScorePercentage { get; private set; }
    public decimal PassingScorePercentage { get; private set; } = 80.0m;
    public bool IsCertified { get; private set; }
    public string CertificationReference { get; private set; }
    public DateTime CertifiedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public string CertifiedByOfficerId { get; private set; }

    #pragma warning disable CS8618
    protected EmployeeTrainingCertification() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    private EmployeeTrainingCertification(
        Guid id,
        string employeeId,
        string employeeName,
        string branchCode,
        string userGroup,
        TrainingCurriculumType curriculumType,
        DateTime trainingDate,
        decimal scorePercentage,
        string certificationReference,
        string certifiedByOfficerId) : base(id)
    {
        EmployeeId = employeeId.Trim();
        EmployeeName = employeeName.Trim();
        BranchCode = branchCode.Trim();
        UserGroup = userGroup.Trim();
        CurriculumType = curriculumType;
        TrainingDate = trainingDate;
        ScorePercentage = scorePercentage;
        IsCertified = true;
        CertificationReference = certificationReference.Trim();
        CertifiedAtUtc = DateTime.UtcNow;
        // Strict 365-day validity
        ExpiresAtUtc = trainingDate.AddDays(365);
        CertifiedByOfficerId = certifiedByOfficerId.Trim();
    }

    public static EmployeeTrainingCertification GrantCertification(
        string employeeId,
        string employeeName,
        string branchCode,
        string userGroup,
        TrainingCurriculumType curriculumType,
        decimal scorePercentage,
        string certificationReference,
        string certifiedByOfficerId,
        DateTime trainingDate)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            throw new ArgumentException("Employee ID is required.", nameof(employeeId));
        if (string.IsNullOrWhiteSpace(employeeName))
            throw new ArgumentException("Employee name is required.", nameof(employeeName));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(certificationReference))
            throw new ArgumentException("Certification reference is required.", nameof(certificationReference));
        if (string.IsNullOrWhiteSpace(certifiedByOfficerId))
            throw new ArgumentException("Certified by officer ID is required.", nameof(certifiedByOfficerId));

        if (scorePercentage < 80.0m)
        {
            throw new InvariantViolationException(
                "CertificationPassingScoreInvariant",
                "BRPD Circular 12/2024 (12.1.2, 12.1.4)",
                $"Employee '{employeeName}' scored {scorePercentage:F1}%. Minimum 80.0% required to grant statutory certification for '{curriculumType}'.");
        }

        return new EmployeeTrainingCertification(
            Guid.NewGuid(),
            employeeId,
            employeeName,
            branchCode,
            userGroup,
            curriculumType,
            trainingDate,
            scorePercentage,
            certificationReference,
            certifiedByOfficerId);
    }

    public CertificationStatus EvaluateStatus(DateTime asOfUtc)
    {
        if (asOfUtc > ExpiresAtUtc)
            return CertificationStatus.Lapsed;

        if (asOfUtc >= ExpiresAtUtc.AddDays(-30))
            return CertificationStatus.ExpiringSoon;

        return CertificationStatus.Active;
    }
}
