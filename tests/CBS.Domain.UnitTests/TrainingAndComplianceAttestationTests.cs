using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Training;
using CBS.Domain.Training.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class TrainingAndComplianceAttestationTests
{
    [Fact]
    public void TrainingSimulationSession_ShouldRecordStepsAndEvaluatePassingScore_WhenScoreAtOrAbove80Percent()
    {
        // Arrange
        var session = TrainingSimulationSession.StartSession(
            "TRN-USR-01",
            "Sagir Ahmed",
            "0101",
            SimulationScenarioType.CashCounterBalancing);

        session.TotalSteps.Should().Be(4);
        session.CurrentStepNumber.Should().Be(1);
        session.Status.Should().Be(SimulationSessionStatus.InProgress);

        // Act - Record steps
        session.RecordStepResult(1, "Verify opening drawer balance", true, "Drawer balanced correctly with Head Cashier.");
        session.RecordStepResult(2, "Receive cash deposit and count", true, "Note bundle counting verified.");
        session.RecordStepResult(3, "Generate deposit counter voucher", true, "Dual-entry voucher posted to GL.");
        session.RecordStepResult(4, "Transfer closing cash to main vault", true, "Dual custody vault transfer completed.");

        session.CompleteSession(88.5m);

        // Assert
        session.Status.Should().Be(SimulationSessionStatus.Passed);
        session.ScorePercentage.Should().Be(88.5m);
        session.EndTimeUtc.Should().NotBeNull();
        session.SimulationNotes.Should().Contain("88.5%");
    }

    [Fact]
    public void TrainingSimulationSession_ShouldMarkFailed_WhenScoreBelow80Percent()
    {
        // Arrange
        var session = TrainingSimulationSession.StartSession(
            "TRN-USR-02",
            "Trainee Officer",
            "0102",
            SimulationScenarioType.TbmlAnomalyResolution);

        // Act
        session.RecordStepResult(1, "Review shipping invoice price", false, "Overlooked 45% unit price variance against BB benchmark.");
        session.CompleteSession(65.0m);

        // Assert
        session.Status.Should().Be(SimulationSessionStatus.Failed);
        session.ScorePercentage.Should().Be(65.0m);
        session.EndTimeUtc.Should().NotBeNull();
    }

    [Fact]
    public void TrainingSimulationSession_ShouldRejectStepRecord_WhenSessionNotInProgress()
    {
        // Arrange
        var session = TrainingSimulationSession.StartSession(
            "TRN-USR-03",
            "Trainee Officer",
            "0101",
            SimulationScenarioType.HighValueChequeReturn);

        session.CompleteSession(90.0m);

        // Act
        var act = () => session.RecordStepResult(1, "Attempt late step", true, "N/A");

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Cannot record steps on a simulation session in 'Passed' state*");
    }

    [Fact]
    public void EmployeeTrainingCertification_ShouldGrantCertification_WhenScoreAtOrAbove80Percent_AndEnforce365DayLifecycle()
    {
        // Arrange
        var trainingDate = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var cert = EmployeeTrainingCertification.GrantCertification(
            "EMP-10023",
            "Rafiqul Islam",
            "0101",
            "Branch Cashier",
            TrainingCurriculumType.GeneralBankingCashAndTellerSecurity,
            85.0m,
            "CERT-GB-2026-0042",
            "MGR-001",
            trainingDate);

        // Assert
        cert.IsCertified.Should().BeTrue();
        cert.ScorePercentage.Should().Be(85.0m);
        cert.PassingScorePercentage.Should().Be(80.0m);
        cert.ExpiresAtUtc.Should().Be(trainingDate.AddDays(365));

        // Evaluate Status transitions
        cert.EvaluateStatus(trainingDate.AddDays(100)).Should().Be(CertificationStatus.Active);
        cert.EvaluateStatus(trainingDate.AddDays(340)).Should().Be(CertificationStatus.ExpiringSoon);
        cert.EvaluateStatus(trainingDate.AddDays(366)).Should().Be(CertificationStatus.Lapsed);
    }

    [Fact]
    public void EmployeeTrainingCertification_ShouldThrowInvariantViolationException_WhenScoreBelow80Percent()
    {
        // Arrange
        var trainingDate = DateTime.UtcNow;

        // Act
        var act = () => EmployeeTrainingCertification.GrantCertification(
            "EMP-10099",
            "Failed Candidate",
            "0101",
            "Junior Officer",
            TrainingCurriculumType.CyberSecurityAndPhishingDefense,
            79.9m,
            "CERT-CYBER-2026-0099",
            "MGR-001",
            trainingDate);

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Minimum 80.0% required to grant statutory certification*");
    }

    [Fact]
    public void CustomerSecurityBroadcast_ShouldRejectInvalidWindow_WhenEndDatePrecedesStartDate()
    {
        // Act
        var act = () => new CustomerSecurityBroadcast(
            "Critical OTP Alert",
            "Never share OTP.",
            "কখনোই ওটিপি শেয়ার করবেন না।",
            "Phishing",
            SecurityBroadcastChannel.AtmScreenBanner,
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1),
            "ADMIN-01");

        // Assert
        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*Effective end date*cannot precede start date*");
    }

    [Fact]
    public void MasterComplianceAttestation_ShouldRepresentCompleteAuditWithCryptographicSignature()
    {
        // Arrange
        var moduleResults = new List<ModuleComplianceCheckResult>
        {
            new(BbrpdModuleId.Module01_GeneralBanking, "Module 01: General Banking", "Compliant", 10, 10, "10/10 verified"),
            new(BbrpdModuleId.Module12_UserTrainingAndAwareness, "Module 12: User Training & Awareness", "Compliant", 10, 10, "10/10 verified")
        };

        var issueDate = new DateOnly(2026, 11, 11);
        var deadline = new DateOnly(2024, 12, 31);
        string sig = "d41d8cd98f00b204e9800998ecf8427e1234567890abcdef1234567890abcdef";

        // Act
        var attestation = new MasterComplianceAttestation(
            "BB-BRPD12-CERT-001",
            issueDate,
            deadline,
            true,
            100.0m,
            moduleResults,
            sig);

        // Assert
        attestation.IsFullyCompliant.Should().BeTrue();
        attestation.OverallScorePercentage.Should().Be(100.0m);
        attestation.ModuleResults.Should().HaveCount(2);
        attestation.CertificateNumber.Should().Be("BB-BRPD12-CERT-001");
        attestation.ComplianceDeadline.Should().Be(deadline);
        attestation.CryptographicSignature.Should().Be(sig);
    }
}
