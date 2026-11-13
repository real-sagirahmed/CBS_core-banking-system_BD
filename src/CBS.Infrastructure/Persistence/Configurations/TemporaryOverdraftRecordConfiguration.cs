// ARCHITECTURAL INTENT: EF Core configuration for TemporaryOverdraftRecord entity
// adhering to Bangladesh Bank BRPD Circular No. 12 (3.9.1-iii, 3.9.1-iv, 3.9.5-iv).

using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class TemporaryOverdraftRecordConfiguration : IEntityTypeConfiguration<TemporaryOverdraftRecord>
{
    public void Configure(EntityTypeBuilder<TemporaryOverdraftRecord> builder)
    {
        builder.ToTable("continuous_loan_tod_records");

        builder.HasKey(t => t.TodId);
        builder.Property(t => t.TodId).HasColumnName("tod_id").HasMaxLength(32);
        builder.Property(t => t.LoanAccountNumber).HasColumnName("loan_account_number").HasMaxLength(32).IsRequired();
        builder.Property(t => t.SanctionedTodAmount).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("sanctioned_tod_amount_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.RegularLimitAtGrant).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("regular_limit_at_grant_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.EffectiveLimit).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("effective_limit_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.GrantDate).HasColumnName("grant_date").IsRequired();
        builder.Property(t => t.ExpiryDate).HasColumnName("expiry_date").IsRequired();
        builder.Property(t => t.TodPenalInterestRate).HasColumnName("tod_penal_interest_rate").HasPrecision(8, 4).IsRequired();
        builder.Property(t => t.ApprovedByUserId).HasColumnName("approved_by_user_id").HasMaxLength(64).IsRequired();
        builder.Property(t => t.ApprovalReference).HasColumnName("approval_reference").HasMaxLength(64).IsRequired();
        builder.Property(t => t.Reason).HasColumnName("reason").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(32).IsRequired();

        builder.HasIndex(t => t.LoanAccountNumber);
        builder.HasIndex(t => t.Status);
    }
}
