using System.Text.Json;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBS.Infrastructure.Persistence.Configurations;

public class DocumentaryCreditConfiguration : IEntityTypeConfiguration<DocumentaryCredit>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void Configure(EntityTypeBuilder<DocumentaryCredit> builder)
    {
        builder.ToTable("trade_documentary_credits");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("lc_number")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.BranchCode)
            .HasColumnName("branch_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.CustomerDepositAccountNumber)
            .HasColumnName("customer_deposit_account_number")
            .HasMaxLength(20);

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasColumnName("lc_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.ApplicantId)
            .HasColumnName("applicant_id")
            .IsRequired();

        builder.Property(c => c.BeneficiaryName)
            .HasColumnName("beneficiary_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.BeneficiaryAddress)
            .HasColumnName("beneficiary_address")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.BeneficiaryCountry)
            .HasColumnName("beneficiary_country")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.AdvisingBankBic)
            .HasColumnName("advising_bank_bic")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AdvisingBankName)
            .HasColumnName("advising_bank_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Amount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("amount_json")
            .IsRequired();

        builder.Property(c => c.CashMarginAmount)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Money>(v, JsonOptions)!)
            .HasColumnName("cash_margin_json")
            .IsRequired();

        builder.Property(c => c.TolerancePercentage)
            .HasColumnName("tolerance_percentage")
            .HasPrecision(5, 2);

        builder.Property(c => c.IssueDate)
            .HasColumnName("issue_date")
            .IsRequired();

        builder.Property(c => c.ExpiryDate)
            .HasColumnName("expiry_date")
            .IsRequired();

        builder.Property(c => c.ExpiryPlace)
            .HasColumnName("expiry_place")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.LatestShipmentDate)
            .HasColumnName("latest_shipment_date")
            .IsRequired();

        builder.Property(c => c.PortOfLoading)
            .HasColumnName("port_of_loading")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.PortOfDischarge)
            .HasColumnName("port_of_discharge")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.GoodsDescription)
            .HasColumnName("goods_description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(c => c.HsCode)
            .HasConversion(v => v.Code, v => HsCode.Create(v, false, false))
            .HasColumnName("hs_code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Incoterm)
            .HasConversion<string>()
            .HasColumnName("incoterm")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.IrcNumber)
            .HasColumnName("irc_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.IsIrcValid)
            .HasColumnName("is_irc_valid")
            .IsRequired();

        builder.Property(c => c.IsCibDefaulter)
            .HasColumnName("is_cib_defaulter")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Mt700Message)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<SwiftMt700Message>(v, JsonOptions))
            .HasColumnName("mt700_message_json");

        builder.Property(c => c.TbmlResult)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<TbmlScreeningResult>(v, JsonOptions))
            .HasColumnName("tbml_result_json");

        builder.Property(c => c.MasterLcNumber)
            .HasColumnName("master_lc_number")
            .HasMaxLength(64);

        builder.Property(c => c.MasterLcAmount)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<Money>(v, JsonOptions))
            .HasColumnName("master_lc_amount_json");

        builder.Property(c => c.MasterLcShipmentDate)
            .HasColumnName("master_lc_shipment_date");

        builder.Property(c => c.MasterLcExpiryDate)
            .HasColumnName("master_lc_expiry_date");

        builder.Property(c => c.EdfRefinanced)
            .HasColumnName("edf_refinanced")
            .IsRequired();

        builder.Property(c => c.MakerUserId)
            .HasColumnName("maker_user_id")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.CheckerUserId)
            .HasColumnName("checker_user_id")
            .HasMaxLength(50);

        builder.Property(c => c.AuthorizedAtUtc)
            .HasColumnName("authorized_at_utc");

        builder.Ignore(c => c.DomainEvents);

        builder.HasIndex(c => c.BranchCode).HasDatabaseName("ix_trade_lc_branch");
        builder.HasIndex(c => c.Status).HasDatabaseName("ix_trade_lc_status");
        builder.HasIndex(c => c.ApplicantId).HasDatabaseName("ix_trade_lc_applicant");
    }
}
