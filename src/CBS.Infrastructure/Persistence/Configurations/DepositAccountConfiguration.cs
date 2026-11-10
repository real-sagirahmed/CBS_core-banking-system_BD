using CBS.Domain.Accounts;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DepositAccountConfiguration : IEntityTypeConfiguration<DepositAccount>
{
    public void Configure(EntityTypeBuilder<DepositAccount> builder)
    {
        builder.ToTable("deposit_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(v => v.Value, v => AccountNumber.From(v))
            .HasColumnName("account_number")
            .HasMaxLength(13)
            .IsRequired();

        builder.Property(a => a.Cif)
            .HasConversion(v => v.Value, v => new CifNumber(v))
            .HasColumnName("cif_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ProductType)
            .HasConversion<string>()
            .HasColumnName("product_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasColumnName("operating_status")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("balance_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.HighestCreditBalanceInYear)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("highest_credit_balance_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.HoveringCharges)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("hovering_charges_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.OpenedDate)
            .HasColumnName("opened_date")
            .IsRequired();

        builder.Property(a => a.LastCustomerTransactionDate)
            .HasColumnName("last_customer_transaction_date")
            .IsRequired();

        builder.Property(a => a.DormantDate)
            .HasColumnName("dormant_date");

        builder.Property(a => a.HasTin)
            .HasColumnName("has_tin")
            .IsRequired();

        builder.Property(a => a.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.ReactivatedByUserId)
            .HasColumnName("reactivated_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.ReactivatedAtUtc)
            .HasColumnName("reactivated_at_utc");

        builder.Property(a => a.AnnualInterestRate)
            .HasColumnName("annual_interest_rate")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(a => a.AccruedInterestProvision)
            .HasConversion(v => v.Amount, v => Money.Bdt(v))
            .HasColumnName("accrued_interest_bdt")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(a => a.LastInterestCalculationDate)
            .HasColumnName("last_interest_calculation_date");

        builder.Property(a => a.LastInterestCapitalizationDate)
            .HasColumnName("last_interest_capitalization_date");

        builder.Property(a => a.UnclaimedNoticesServedCount)
            .HasColumnName("unclaimed_notices_served_count")
            .IsRequired();

        builder.Property(a => a.LastUnclaimedNoticeDate)
            .HasColumnName("last_unclaimed_notice_date");

        builder.Property(a => a.ReactivationRequestedByUserId)
            .HasColumnName("reactivation_requested_by_user_id")
            .HasMaxLength(50);

        builder.Property(a => a.ReactivationKycDocumentReference)
            .HasColumnName("reactivation_kyc_document_ref")
            .HasMaxLength(100);

        builder.Property(a => a.ReactivationRequestedAtUtc)
            .HasColumnName("reactivation_requested_at_utc");

        builder.Property(a => a.IsReactivationPending)
            .HasColumnName("is_reactivation_pending")
            .IsRequired();

        builder.Ignore(a => a.DomainEvents);

        builder.HasIndex(a => a.Cif).HasDatabaseName("ix_deposit_accounts_cif");
        builder.HasIndex(a => a.BranchCode).HasDatabaseName("ix_deposit_accounts_branch");
        builder.HasIndex(a => a.Status).HasDatabaseName("ix_deposit_accounts_status");
    }
}
