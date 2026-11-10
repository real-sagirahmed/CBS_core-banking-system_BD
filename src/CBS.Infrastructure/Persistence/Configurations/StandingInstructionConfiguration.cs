using CBS.Domain.StandingInstructions;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class StandingInstructionConfiguration : IEntityTypeConfiguration<StandingInstruction>
{
    public void Configure(EntityTypeBuilder<StandingInstruction> builder)
    {
        builder.ToTable("standing_instructions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("instruction_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.SourceAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("source_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(s => s.TargetAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("target_account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasColumnName("type")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(s => s.Frequency)
            .HasConversion<string>()
            .HasColumnName("frequency")
            .HasMaxLength(30)
            .IsRequired();

        builder.OwnsOne(s => s.TransferAmount, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("transfer_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("transfer_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.OwnsOne(s => s.ExecutionFee, m =>
        {
            m.Property(p => p.Amount)
                .HasColumnName("execution_fee_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            m.Property(p => p.Currency)
                .HasConversion(v => v.Code, v => Currency.FromCode(v))
                .HasColumnName("execution_fee_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(s => s.SpecificDayOfMonth)
            .HasColumnName("specific_day_of_month");

        builder.Property(s => s.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(s => s.EndDate)
            .HasColumnName("end_date");

        builder.Property(s => s.NextExecutionDate)
            .HasColumnName("next_execution_date")
            .IsRequired();

        builder.Property(s => s.LastExecutionDate)
            .HasColumnName("last_execution_date");

        builder.Property(s => s.ConsecutiveFailureCount)
            .HasColumnName("consecutive_failure_count")
            .IsRequired();

        builder.Property(s => s.MaxRetryFailures)
            .HasColumnName("max_retry_failures")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.PauseReason)
            .HasColumnName("pause_reason")
            .HasMaxLength(255);

        builder.Property(s => s.ResumeDate)
            .HasColumnName("resume_date");

        builder.Property(s => s.Narrative)
            .HasColumnName("narrative")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(nameof(StandingInstruction.SourceAccountNumber));
        builder.HasIndex(nameof(StandingInstruction.NextExecutionDate));
    }
}
