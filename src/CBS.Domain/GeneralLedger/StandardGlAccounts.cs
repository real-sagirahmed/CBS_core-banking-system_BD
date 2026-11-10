namespace CBS.Domain.GeneralLedger;

// ARCHITECTURAL INTENT: Standard Bangladesh Bank Chart of Accounts (COA) Codes (BRPD Circular 12/2024 Module 1.3).
// Every sub-ledger financial posting must map to an authoritative 9-digit GL account.
public static class StandardGlAccounts
{
    // ASSETS (100000000)
    public const string MainVaultCash = "101010000";
    public const string TellerTillCash = "101020000";
    public const string BangladeshBankSettlement = "102010000";
    public const string BachClearingAdjustment = "102020000";
    public const string NostroForeignCurrencyBalances = "102030000";
    public const string CustomerLoansAdvances = "103010000";
    public const string CustomerLiabilityForLettersOfCredit = "103020000";
    public const string TreasuryBillsHeldForTrading = "104010000";
    public const string TreasuryBondsHeldToMaturity = "104020000";
    public const string CallMoneyPlacementsWithBanks = "105010000";
    public const string CashDiscrepancyShortage = "109010000";

    // LIABILITIES (200000000)
    public const string CustomerCurrentDeposit = "201010000";
    public const string CustomerSavingsDeposit = "201020000";
    public const string SpecialNoticeDeposit = "201030000";
    public const string FixedDepositReceipt = "201040000";
    public const string PayOrderPayable = "202010000";
    public const string StatutorySourceTaxPayable = "203010000";
    public const string StatutoryExciseDutyPayable = "203020000";
    public const string StatutoryVatPayable = "203030000";
    public const string BangladeshBankUnclaimedFunds = "203040000";
    public const string LoanInterestSuspense = "203050000";
    public const string CustomerCashMarginDeposit = "203060000";
    public const string BankAcceptanceAndLcLiability = "204010000";
    public const string CallMoneyBorrowingsFromBanks = "205010000";
    public const string CashDiscrepancyExcess = "209010000";

    // EQUITY & RESERVES (300000000)
    public const string TreasuryRevaluationReserve = "301020000";

    // REVENUE (400000000)
    public const string RemittanceCommissionIncome = "401010000";
    public const string AccountMaintenanceHoveringFeeIncome = "401020000";
    public const string TradeCommissionAndFeeIncome = "401030000";
    public const string LoanInterestIncome = "402010000";
    public const string TreasuryDealingAndTradingGain = "403010000";

    // EXPENSES (500000000)
    public const string DepositInterestExpense = "501010000";
    public const string MoneyMarketInterestExpense = "501020000";
    public const string TreasuryDealingAndTradingLoss = "503010000";
}
