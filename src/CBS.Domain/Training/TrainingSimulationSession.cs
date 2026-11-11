using System.Text.Json;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Training;

// ARCHITECTURAL INTENT: In accordance with Bangladesh Bank BRPD Circular 12/2024 Clause 12.1.5,
// simulation and scenario-based training sessions MUST be strictly sandboxed.
// Simulation transactions and virtual vouchers are strictly tagged with IsSimulation = true,
// cannot be posted to real customer accounts or live General Ledger books,
// and do not trigger actual clearing, SWIFT, or payment switch network transmissions.
public class TrainingSimulationSession : AggregateRoot<Guid>
{
    public string TraineeUserId { get; private set; }
    public string TraineeName { get; private set; }
    public string BranchCode { get; private set; }
    public SimulationScenarioType ScenarioType { get; private set; }
    public DateTime StartTimeUtc { get; private set; }
    public DateTime? EndTimeUtc { get; private set; }
    public decimal ScorePercentage { get; private set; }
    public SimulationSessionStatus Status { get; private set; }
    public int CurrentStepNumber { get; private set; }
    public int TotalSteps { get; private set; }
    public string CompletedStepsJson { get; private set; }
    public string SimulationNotes { get; private set; }

    #pragma warning disable CS8618
    protected TrainingSimulationSession() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    private TrainingSimulationSession(
        Guid id,
        string traineeUserId,
        string traineeName,
        string branchCode,
        SimulationScenarioType scenarioType,
        int totalSteps) : base(id)
    {
        TraineeUserId = traineeUserId.Trim();
        TraineeName = traineeName.Trim();
        BranchCode = branchCode.Trim();
        ScenarioType = scenarioType;
        StartTimeUtc = DateTime.UtcNow;
        ScorePercentage = 0m;
        Status = SimulationSessionStatus.InProgress;
        CurrentStepNumber = 1;
        TotalSteps = totalSteps;
        CompletedStepsJson = "[]";
        SimulationNotes = $"Sandbox session initialized for scenario '{scenarioType}'.";
    }

    public static TrainingSimulationSession StartSession(
        string traineeUserId,
        string traineeName,
        string branchCode,
        SimulationScenarioType scenarioType)
    {
        if (string.IsNullOrWhiteSpace(traineeUserId))
            throw new ArgumentException("Trainee user ID is required.", nameof(traineeUserId));
        if (string.IsNullOrWhiteSpace(traineeName))
            throw new ArgumentException("Trainee name is required.", nameof(traineeName));
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));

        int standardSteps = scenarioType switch
        {
            SimulationScenarioType.CashCounterBalancing => 4,
            SimulationScenarioType.HighValueChequeReturn => 5,
            SimulationScenarioType.LoanDisbursementAgainstLien => 5,
            SimulationScenarioType.ImportLcIssuanceAndMargin => 6,
            SimulationScenarioType.TbmlAnomalyResolution => 4,
            SimulationScenarioType.UnclaimedDeposit10YearTransfer => 4,
            SimulationScenarioType.EodBatchCutoffFailureHandling => 5,
            _ => 5
        };

        return new TrainingSimulationSession(
            Guid.NewGuid(),
            traineeUserId,
            traineeName,
            branchCode,
            scenarioType,
            standardSteps);
    }

    public void RecordStepResult(int stepNumber, string actionName, bool isCorrect, string feedback)
    {
        if (Status != SimulationSessionStatus.InProgress)
        {
            throw new InvariantViolationException(
                "InactiveSimulationSessionInvariant",
                "BRPD Circular 12/2024 (12.1.5)",
                $"Cannot record steps on a simulation session in '{Status}' state.");
        }

        if (stepNumber < 1 || stepNumber > TotalSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(stepNumber), $"Step number must be between 1 and {TotalSteps}.");
        }

        var steps = string.IsNullOrWhiteSpace(CompletedStepsJson) || CompletedStepsJson == "[]"
            ? new List<Dictionary<string, object>>()
            : JsonSerializer.Deserialize<List<Dictionary<string, object>>>(CompletedStepsJson) ?? [];

        steps.Add(new Dictionary<string, object>
        {
            ["stepNumber"] = stepNumber,
            ["actionName"] = actionName,
            ["isCorrect"] = isCorrect,
            ["feedback"] = feedback,
            ["recordedAtUtc"] = DateTime.UtcNow.ToString("O")
        });

        CompletedStepsJson = JsonSerializer.Serialize(steps);
        CurrentStepNumber = Math.Min(TotalSteps, stepNumber + 1);
    }

    public void CompleteSession(decimal finalScore)
    {
        if (Status != SimulationSessionStatus.InProgress)
        {
            throw new InvariantViolationException(
                "InactiveSimulationSessionInvariant",
                "BRPD Circular 12/2024 (12.1.5)",
                $"Simulation session '{Id}' is already '{Status}'.");
        }

        EndTimeUtc = DateTime.UtcNow;
        ScorePercentage = Math.Clamp(finalScore, 0m, 100m);

        // Standard passing score is 80.0%
        Status = ScorePercentage >= 80.0m
            ? SimulationSessionStatus.Passed
            : SimulationSessionStatus.Failed;

        SimulationNotes = $"Session finished with score {ScorePercentage:F1}%. Status: {Status}.";
    }
}
