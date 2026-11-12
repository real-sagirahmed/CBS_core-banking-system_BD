// ARCHITECTURAL INTENT: EF Core configuration for LoanWriteOffRecord aggregate root.
// Maps loan write-offs, 100% provision surrenders, Artha Rin Adalat court cases,
// and post-writeoff cash recoveries per BRPD Circular 01/2013 & 08/2023.

using CBS.Domain.Lending;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public sealed class LoanWriteOffRecordConfiguration : IEntityTypeConfiguration<LoanWriteOffRecord>
{
    public void Configure(EntityTypeBuilder<LoanWriteOffRecord> builder)
    {
        builder.ToTable("loan_write_off_records");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.LoanAccountNumber)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("loan_account_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.CustomerCif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("customer_cif")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(w => w.WrittenOffPrincipal)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("written_off_principal_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(w => w.WrittenOffInterestSuspense)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("written_off_interest_suspense_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(w => w.MaintainedSpecificProvision)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("maintained_specific_provision_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(w => w.CaseType)
            .HasConversion<string>()
            .HasColumnName("legal_case_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.ArthaRinCaseNumber)
            .HasColumnName("artha_rin_case_number")
            .HasMaxLength(100);

        builder.Property(w => w.ArthaRinFilingDate)
            .HasColumnName("artha_rin_filing_date");

        builder.Property(w => w.BoardResolutionNumber)
            .HasColumnName("board_resolution_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(w => w.BoardApprovalDate)
            .HasColumnName("board_approval_date")
            .IsRequired();

        builder.Property(w => w.WriteOffDate)
            .HasColumnName("write_off_date")
            .IsRequired();

        builder.Property(w => w.CumulativeRecoveredAmount)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("cumulative_recovered_amount_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(w => w.ApprovedByUserId)
            .HasColumnName("approved_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Ignore(w => w.DomainEvents);

        builder.HasIndex(w => w.LoanAccountNumber).HasDatabaseName("ix_loan_write_offs_account");
        builder.HasIndex(w => w.CustomerCif).HasDatabaseName("ix_loan_write_offs_cif");
    }
}
