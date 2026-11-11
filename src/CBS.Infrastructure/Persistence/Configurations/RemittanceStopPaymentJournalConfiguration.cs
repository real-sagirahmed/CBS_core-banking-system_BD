using CBS.Domain.BranchCash;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class RemittanceStopPaymentJournalConfiguration : IEntityTypeConfiguration<RemittanceStopPaymentJournal>
{
    public void Configure(EntityTypeBuilder<RemittanceStopPaymentJournal> builder)
    {
        builder.ToTable("remittance_stop_payment_journals");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnName("journal_id")
            .IsRequired();

        builder.Property(j => j.InstrumentType)
            .HasColumnName("instrument_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.InstrumentNumber)
            .HasColumnName("instrument_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(j => j.ActionType)
            .HasColumnName("action_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(j => j.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(j => j.AuthorizedByUserId)
            .HasColumnName("authorized_by_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.ActionDateUtc)
            .HasColumnName("action_date_utc")
            .IsRequired();

        builder.Property(j => j.IndemnityBondReference)
            .HasColumnName("indemnity_bond_ref")
            .HasMaxLength(100);

        builder.HasIndex(j => j.InstrumentNumber);
        builder.HasIndex(j => j.BranchCode);
        builder.HasIndex(j => j.ActionDateUtc);
    }
}
