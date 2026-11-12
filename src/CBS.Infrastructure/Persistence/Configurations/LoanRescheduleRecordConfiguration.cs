// ARCHITECTURAL INTENT: EF Core configuration for LoanRescheduleRecord aggregate root.
// Maps loan rescheduling history, mandatory down payment checks, board resolution numbers,
// and interest suspense adjustments per BRPD Circular 12 (4.1.12, 4.2.28) and BRPD 16/2020 & 04/2023.

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class LoanRescheduleRecordConfiguration : IEntityTypeConfiguration<LoanRescheduleRecord>
{
    public void Configure(EntityTypeBuilder<LoanRescheduleRecord> builder)
    {
        builder.ToTable("loan_reschedule_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.LoanAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("loan_account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.RescheduleIteration)
            .HasColumnName("reschedule_iteration")
            .IsRequired();

        builder.Property(r => r.DownPaymentPaid)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("down_payment_paid_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.RequiredDownPayment)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("required_down_payment_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.ApprovalAuthority)
            .HasConversion<string>()
            .HasColumnName("approval_authority")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.BoardResolutionReference)
            .HasColumnName("board_resolution_reference")
            .HasMaxLength(100);

        builder.Property(r => r.BoardApprovalDate)
            .HasColumnName("board_approval_date");

        builder.Property(r => r.InterestWaiver)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("interest_waiver_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.OldMaturityDate)
            .HasColumnName("old_maturity_date")
            .IsRequired();

        builder.Property(r => r.NewMaturityDate)
            .HasColumnName("new_maturity_date")
            .IsRequired();

        builder.Property(r => r.OldInterestRate)
            .HasColumnName("old_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.NewInterestRate)
            .HasColumnName("new_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.EffectiveDate)
            .HasColumnName("effective_date")
            .IsRequired();

        builder.Property(r => r.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.LoanAccountNumber).HasDatabaseName("ix_loan_reschedules_account");
    }
}
