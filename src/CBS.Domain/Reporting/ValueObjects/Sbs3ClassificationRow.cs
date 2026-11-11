using CBS.Domain.Enums;

namespace CBS.Domain.Reporting.ValueObjects;

// ARCHITECTURAL INTENT: In accordance with Bangladesh Bank BRPD Master Circular on Loan Classification and Provisioning,
// statutory provision rates are strictly deterministic and cannot be overridden:
// - Standard (Unclassified): 1.0%
// - Special Mention Account (SMA): 2.0%
// - Sub-Standard (SS): 20.0%
// - Doubtful (DF): 50.0%
// - Bad/Loss (BL): 100.0% (after deducting eligible collateral value)
// Interest on classified loans (SS, DF, BL) MUST be held in Interest Suspense and excluded from bank income.
public sealed record Sbs3ClassificationRow
{
    public string AccountNumber { get; init; }
    public string BorrowerCif { get; init; }
    public string BorrowerName { get; init; }
    public EconomicSectorCategory Sector { get; init; }
    public decimal SanctionLimit { get; init; }
    public decimal OutstandingBalance { get; init; }
    public decimal OverdueAmount { get; init; }
    public int DaysPastDue { get; init; }
    public LoanClassificationGrade ClassificationGrade { get; init; }
    public decimal EligibleCollateralValue { get; init; }
    public decimal BaseForProvision { get; init; }
    public decimal RequiredProvisionRate { get; init; }
    public decimal RequiredProvisionAmount { get; init; }
    public decimal MaintainedProvisionAmount { get; init; }
    public decimal InterestSuspenseBalance { get; init; }

    public bool IsClassified => ClassificationGrade is LoanClassificationGrade.SubStandard_SS 
        or LoanClassificationGrade.Doubtful_DF 
        or LoanClassificationGrade.BadLoss_BL;

    #pragma warning disable CS8618
    public Sbs3ClassificationRow() { }
    #pragma warning restore CS8618

    public Sbs3ClassificationRow(
        string accountNumber,
        string borrowerCif,
        string borrowerName,
        EconomicSectorCategory sector,
        decimal sanctionLimit,
        decimal outstandingBalance,
        decimal overdueAmount,
        int daysPastDue,
        LoanClassificationGrade classificationGrade,
        decimal eligibleCollateralValue,
        decimal interestSuspenseBalance,
        decimal? maintainedProvisionAmount = null)
    {
        AccountNumber = accountNumber ?? throw new ArgumentNullException(nameof(accountNumber));
        BorrowerCif = borrowerCif ?? throw new ArgumentNullException(nameof(borrowerCif));
        BorrowerName = borrowerName ?? throw new ArgumentNullException(nameof(borrowerName));
        Sector = sector;
        SanctionLimit = sanctionLimit;
        OutstandingBalance = outstandingBalance;
        OverdueAmount = overdueAmount;
        DaysPastDue = daysPastDue;
        ClassificationGrade = classificationGrade;
        EligibleCollateralValue = Math.Max(0m, eligibleCollateralValue);
        InterestSuspenseBalance = interestSuspenseBalance;

        // Base for provision: For classified loans, collateral is deducted from outstanding before provision.
        // For unclassified, collateral does not reduce the base for general provision under BB guidelines.
        if (classificationGrade is LoanClassificationGrade.SubStandard_SS 
            or LoanClassificationGrade.Doubtful_DF 
            or LoanClassificationGrade.BadLoss_BL)
        {
            BaseForProvision = Math.Max(0m, OutstandingBalance - EligibleCollateralValue);
        }
        else
        {
            BaseForProvision = OutstandingBalance;
        }

        RequiredProvisionRate = classificationGrade switch
        {
            LoanClassificationGrade.Standard => 0.01m,                  // 1% General
            LoanClassificationGrade.SpecialMentionAccount_SMA => 0.02m, // 2% General
            LoanClassificationGrade.SubStandard_SS => 0.20m,            // 20% Specific
            LoanClassificationGrade.Doubtful_DF => 0.50m,               // 50% Specific
            LoanClassificationGrade.BadLoss_BL => 1.00m,                // 100% Specific
            _ => 0.01m
        };

        RequiredProvisionAmount = Math.Round(BaseForProvision * RequiredProvisionRate, 2, MidpointRounding.AwayFromZero);
        MaintainedProvisionAmount = maintainedProvisionAmount ?? RequiredProvisionAmount;
    }
}
