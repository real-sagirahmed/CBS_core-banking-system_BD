using CBS.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TrainingSimulationSessionConfiguration : IEntityTypeConfiguration<TrainingSimulationSession>
{
    public void Configure(EntityTypeBuilder<TrainingSimulationSession> builder)
    {
        builder.ToTable("training_simulation_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(s => s.TraineeUserId)
            .HasColumnName("trainee_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.TraineeName)
            .HasColumnName("trainee_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.ScenarioType)
            .HasConversion<string>()
            .HasColumnName("scenario_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.StartTimeUtc)
            .HasColumnName("start_time_utc")
            .IsRequired();

        builder.Property(s => s.EndTimeUtc)
            .HasColumnName("end_time_utc");

        builder.Property(s => s.ScorePercentage)
            .HasColumnName("score_percentage")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.CurrentStepNumber)
            .HasColumnName("current_step_number")
            .IsRequired();

        builder.Property(s => s.TotalSteps)
            .HasColumnName("total_steps")
            .IsRequired();

        builder.Property(s => s.CompletedStepsJson)
            .HasColumnName("completed_steps_json")
            .IsRequired();

        builder.Property(s => s.SimulationNotes)
            .HasColumnName("simulation_notes")
            .HasMaxLength(500);

        builder.HasIndex(s => s.TraineeUserId)
            .HasDatabaseName("ix_training_simulations_trainee");
    }
}
