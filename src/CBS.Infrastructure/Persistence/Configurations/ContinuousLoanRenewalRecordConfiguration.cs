// ARCHITECTURAL INTENT: EF Core configuration for ContinuousLoanRenewalRecord entity
// adhering to Bangladesh Bank BRPD Circular No. 12 (4.1.13).

using CBS.Domain.Lending.Continuous;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class ContinuousLoanRenewalRecordConfiguration : IEntityTypeConfiguration<ContinuousLoanRenewalRecord>
{
    public void Configure(EntityTypeBuilder<ContinuousLoanRenewalRecord> builder)
    {
        builder.ToTable("continuous_loan_renewal_records");

        builder.HasKey(r => r.RenewalId);
        builder.Property(r => r.RenewalId).HasColumnName("renewal_id").HasMaxLength(32);
        builder.Property(r => r.LoanAccountNumber).HasColumnName("loan_account_number").HasMaxLength(32).IsRequired();
        builder.Property(r => r.RenewalDate).HasColumnName("renewal_date").IsRequired();

        builder.Property(r => r.PreviousLimit).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("previous_limit_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.NewLimit).HasConversion(v => v.Amount, v => Money.Bdt(v)).HasColumnName("new_limit_bdt").HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.PreviousExpiryDate).HasColumnName("previous_expiry_date").IsRequired();
        builder.Property(r => r.NewExpiryDate).HasColumnName("new_expiry_date").IsRequired();

        builder.Property(r => r.PreviousInterestRate).HasColumnName("previous_interest_rate").HasPrecision(8, 4).IsRequired();
        builder.Property(r => r.NewInterestRate).HasColumnName("new_interest_rate").HasPrecision(8, 4).IsRequired();

        builder.Property(r => r.ApprovedByUserId).HasColumnName("approved_by_user_id").HasMaxLength(64).IsRequired();
        builder.Property(r => r.SanctionAdviceReference).HasColumnName("sanction_advice_reference").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Remarks).HasColumnName("remarks").HasMaxLength(500);

        builder.HasIndex(r => r.LoanAccountNumber);
        builder.HasIndex(r => r.RenewalDate);
    }
}
