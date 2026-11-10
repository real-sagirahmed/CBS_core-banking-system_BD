using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfChartOfAccountsRepository : IChartOfAccountsRepository
{
    private readonly CbsDbContext _dbContext;

    public EfChartOfAccountsRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ChartOfAccount?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChartOfAccounts
            .FirstOrDefaultAsync(c => c.Id == code, cancellationToken);
    }

    public async Task<IReadOnlyList<ChartOfAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChartOfAccounts
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ChartOfAccount account, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChartOfAccounts.AddAsync(account, cancellationToken);
    }

    public async Task SeedDefaultAccountsAsync(CancellationToken cancellationToken = default)
    {
        if (await _dbContext.ChartOfAccounts.AnyAsync(cancellationToken))
        {
            return;
        }

        var defaults = new List<ChartOfAccount>
        {
            // Assets (100000000)
            new(StandardGlAccounts.MainVaultCash, "Main Vault Cash Account", AccountCategory.Asset),
            new(StandardGlAccounts.TellerTillCash, "Teller Cash Till Account", AccountCategory.Asset),
            new(StandardGlAccounts.BangladeshBankSettlement, "Bangladesh Bank Clearing Settlement Account", AccountCategory.Asset),
            new(StandardGlAccounts.BachClearingAdjustment, "BACH Clearing Adjustment Account", AccountCategory.Asset),
            new(StandardGlAccounts.NostroForeignCurrencyBalances, "Nostro Foreign Currency Balances with Correspondent Banks", AccountCategory.Asset),
            new(StandardGlAccounts.CustomerLoansAdvances, "Customer Loans and Advances", AccountCategory.Asset),
            new(StandardGlAccounts.CustomerLiabilityForLettersOfCredit, "Customer Liability for Letters of Credit (Contingent)", AccountCategory.Asset),
            new(StandardGlAccounts.TreasuryBillsHeldForTrading, "Government Treasury Bills (Held For Trading - HFT)", AccountCategory.Asset),
            new(StandardGlAccounts.TreasuryBondsHeldToMaturity, "Government Treasury Bonds (Held To Maturity - HTM)", AccountCategory.Asset),
            new(StandardGlAccounts.CallMoneyPlacementsWithBanks, "Interbank Call Money Placements (Lending)", AccountCategory.Asset),
            new(StandardGlAccounts.CashDiscrepancyShortage, "Cash Discrepancy - Teller Shortage", AccountCategory.Asset),

            // Liabilities (200000000)
            new(StandardGlAccounts.CustomerCurrentDeposit, "Customer Current Deposits (Demand)", AccountCategory.Liability),
            new(StandardGlAccounts.CustomerSavingsDeposit, "Customer Savings Deposits (Time/Demand)", AccountCategory.Liability),
            new(StandardGlAccounts.SpecialNoticeDeposit, "Special Notice Deposits (SND)", AccountCategory.Liability),
            new(StandardGlAccounts.FixedDepositReceipt, "Fixed Deposit Receipts (FDR)", AccountCategory.Liability),
            new(StandardGlAccounts.PayOrderPayable, "Payment Orders (PO) Payable Liability", AccountCategory.Liability),
            new(StandardGlAccounts.StatutorySourceTaxPayable, "Statutory Source Tax (AIT) Payable", AccountCategory.Liability),
            new(StandardGlAccounts.StatutoryExciseDutyPayable, "Statutory Excise Duty Payable", AccountCategory.Liability),
            new(StandardGlAccounts.StatutoryVatPayable, "Statutory 15% VAT Payable", AccountCategory.Liability),
            new(StandardGlAccounts.BangladeshBankUnclaimedFunds, "Bangladesh Bank Unclaimed Funds Government Account", AccountCategory.Liability),
            new(StandardGlAccounts.LoanInterestSuspense, "Loan Interest Suspense Liability Account", AccountCategory.Liability),
            new(StandardGlAccounts.CustomerCashMarginDeposit, "Customer Cash Margin on Letters of Credit & Guarantees", AccountCategory.Liability),
            new(StandardGlAccounts.BankAcceptanceAndLcLiability, "Bank Acceptance and Letters of Credit Liability (Contingent)", AccountCategory.Liability),
            new(StandardGlAccounts.CallMoneyBorrowingsFromBanks, "Interbank Call Money Borrowings", AccountCategory.Liability),
            new(StandardGlAccounts.CashDiscrepancyExcess, "Cash Discrepancy - Teller Excess", AccountCategory.Liability),

            // Equity & Reserves (300000000)
            new(StandardGlAccounts.TreasuryRevaluationReserve, "Treasury Securities Revaluation Reserve (DOS Circular 05/2014)", AccountCategory.Equity),

            // Income (400000000)
            new(StandardGlAccounts.RemittanceCommissionIncome, "Remittance Issuance Commission Income", AccountCategory.Income),
            new(StandardGlAccounts.AccountMaintenanceHoveringFeeIncome, "Account Maintenance & Hovering Fee Income", AccountCategory.Income),
            new(StandardGlAccounts.TradeCommissionAndFeeIncome, "Trade Services Commission and Fee Income", AccountCategory.Income),
            new(StandardGlAccounts.LoanInterestIncome, "Loan and Advance Interest Income", AccountCategory.Income),
            new(StandardGlAccounts.TreasuryDealingAndTradingGain, "Treasury Dealing & Securities Trading Gain", AccountCategory.Income),

            // Expenses (500000000)
            new(StandardGlAccounts.DepositInterestExpense, "Customer Deposit Interest Expense", AccountCategory.Expense),
            new(StandardGlAccounts.MoneyMarketInterestExpense, "Money Market Borrowing Interest Expense", AccountCategory.Expense),
            new(StandardGlAccounts.TreasuryDealingAndTradingLoss, "Treasury Dealing & Securities Trading Loss", AccountCategory.Expense)
        };

        await _dbContext.ChartOfAccounts.AddRangeAsync(defaults, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
