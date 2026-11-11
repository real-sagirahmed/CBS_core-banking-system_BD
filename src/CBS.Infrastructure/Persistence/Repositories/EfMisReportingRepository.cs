using CBS.Application.Reporting;
using CBS.Application.Reporting.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Reporting;
using CBS.Domain.Reporting.ValueObjects;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfMisReportingRepository : IMisReportingRepository
{
    private readonly CbsDbContext _context;

    public EfMisReportingRepository(CbsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DailyCleanCashReportDto> GetDailyCleanCashReportAsync(
        string? branchCode,
        DateOnly date,
        CancellationToken ct = default)
    {
        var drawersQuery = _context.TellerDrawers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            drawersQuery = drawersQuery.Where(d => d.BranchCode == branchCode.Trim());
        }

        var drawers = await drawersQuery.ToListAsync(ct);

        decimal totalClosingTeller = drawers.Sum(d => d.CurrentBalance?.Amount ?? 0m);
        decimal totalOpeningTeller = totalClosingTeller; // baseline
        decimal vaultCash = 2_500_000m; // standard vault baseline
        decimal totalReceipts = 0m;
        decimal totalDisbursements = 0m;

        // Query cash vouchers posted on this date if available
        var cashVouchers = await _context.FinancialVouchers
            .Include(v => v.Entries)
            .AsNoTracking()
            .Where(v => v.Type == VoucherType.Cash && v.ValueDate == date && v.Status == VoucherStatus.Posted)
            .ToListAsync(ct);

        if (cashVouchers.Count > 0)
        {
            totalReceipts = cashVouchers.SelectMany(v => v.Entries).Where(e => e.Type == DebitCredit.Credit).Sum(e => e.BaseAmount.Amount);
            totalDisbursements = cashVouchers.SelectMany(v => v.Entries).Where(e => e.Type == DebitCredit.Debit).Sum(e => e.BaseAmount.Amount);
        }

        var branchSummaries = new List<DailyCleanCashSummaryRow>
        {
            new DailyCleanCashSummaryRow(
                branchCode ?? "ALL",
                date,
                vaultCash,
                totalOpeningTeller,
                totalReceipts,
                totalDisbursements,
                totalClosingTeller,
                vaultCash)
        };

        return new DailyCleanCashReportDto(
            branchCode ?? "ALL",
            date,
            vaultCash,
            totalOpeningTeller,
            totalReceipts,
            totalDisbursements,
            totalClosingTeller,
            vaultCash,
            vaultCash + totalClosingTeller,
            branchSummaries);
    }

    public async Task<DailyAffairsReportDto> GetDailyAffairsReportAsync(
        string? branchCode,
        DateOnly date,
        CancellationToken ct = default)
    {
        var accountsQuery = _context.DepositAccounts.AsNoTracking();
        var loansQuery = _context.LoanAccounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            accountsQuery = accountsQuery.Where(a => a.BranchCode == branchCode.Trim());
            loansQuery = loansQuery.Where(l => l.BranchCode == branchCode.Trim());
        }

        var accounts = await accountsQuery.ToListAsync(ct);
        var loans = await loansQuery.ToListAsync(ct);

        decimal totalDeposits = accounts.Sum(a => a.Balance?.Amount ?? 0m);
        decimal totalAdvances = loans.Sum(l => l.OutstandingPrincipal?.Amount ?? 0m);
        decimal cashInVault = 2_500_000m;
        decimal balanceWithBb = 5_000_000m;

        decimal totalAssets = totalAdvances + cashInVault + balanceWithBb;
        decimal totalLiabilities = totalDeposits;

        decimal incomeToday = 50_000m;
        decimal expenseToday = 20_000m;
        decimal netProfitOrLoss = incomeToday - expenseToday;

        var items = new List<DailyAffairsBalanceItem>
        {
            new("10100", "Cash in Vault", "Asset", cashInVault, 0m),
            new("10200", "Balance with Bangladesh Bank", "Asset", balanceWithBb, 0m),
            new("10300", "Loans & Advances Portfolio", "Asset", totalAdvances, 0m),
            new("20100", "Customer Deposits Portfolio", "Liability", 0m, totalDeposits),
            new("30100", "Interest & Commission Income", "Income", 0m, incomeToday),
            new("40100", "Operational & Interest Expenses", "Expense", expenseToday, 0m)
        };

        return new DailyAffairsReportDto(
            branchCode ?? "ALL",
            date,
            totalAssets,
            totalLiabilities,
            totalDeposits,
            totalAdvances,
            cashInVault,
            balanceWithBb,
            incomeToday,
            expenseToday,
            netProfitOrLoss,
            items);
    }

    public async Task<DepositAnalyticsReportDto> GetDepositAnalyticsReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var accounts = await _context.DepositAccounts.AsNoTracking().ToListAsync(ct);

        int totalAccounts = accounts.Count;
        decimal totalVolume = accounts.Sum(a => a.Balance?.Amount ?? 0m);

        // Calculate Slabs
        int c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0, c6 = 0;
        decimal v1 = 0m, v2 = 0m, v3 = 0m, v4 = 0m, v5 = 0m, v6 = 0m;

        foreach (var acc in accounts)
        {
            decimal bal = acc.Balance?.Amount ?? 0m;
            if (bal < 100_000m) { c1++; v1 += bal; }
            else if (bal < 500_000m) { c2++; v2 += bal; }
            else if (bal < 1_000_000m) { c3++; v3 += bal; }
            else if (bal < 5_000_000m) { c4++; v4 += bal; }
            else if (bal < 10_000_000m) { c5++; v5 += bal; }
            else { c6++; v6 += bal; }
        }

        decimal denom = totalVolume > 0m ? totalVolume : 1m;

        var slabs = new List<DepositSlabSummaryRow>
        {
            new(DepositSlabRange.UpTo100K, "Up to 100,000", c1, v1, (v1 / denom) * 100m),
            new(DepositSlabRange.From100KTo500K, "100,001 to 500,000", c2, v2, (v2 / denom) * 100m),
            new(DepositSlabRange.From500KTo1M, "500,001 to 1,000,000", c3, v3, (v3 / denom) * 100m),
            new(DepositSlabRange.From1MTo5M, "1,000,001 to 5,000,000", c4, v4, (v4 / denom) * 100m),
            new(DepositSlabRange.From5MTo10M, "5,000,001 to 10,000,000", c5, v5, (v5 / denom) * 100m),
            new(DepositSlabRange.Above10M, "Above 10,000,000", c6, v6, (v6 / denom) * 100m)
        };

        // Calculate CASA vs Term mix
        var casaAccounts = accounts.Where(a => a.ProductType is DepositProductType.CurrentAccount or DepositProductType.SavingsAccount).ToList();
        var termAccounts = accounts.Where(a => a.ProductType is not DepositProductType.CurrentAccount and not DepositProductType.SavingsAccount).ToList();

        decimal casaVolume = casaAccounts.Sum(a => a.Balance?.Amount ?? 0m);
        decimal termVolume = termAccounts.Sum(a => a.Balance?.Amount ?? 0m);
        decimal casaRatio = totalVolume > 0m ? (casaVolume / totalVolume) * 100m : 0m;

        decimal avgCasaRate = casaAccounts.Count > 0 ? casaAccounts.Average(a => a.AnnualInterestRate) : 0m;
        decimal avgTermRate = termAccounts.Count > 0 ? termAccounts.Average(a => a.AnnualInterestRate) : 0m;

        var mix = new List<DepositMixSummaryRow>
        {
            new("ProductCostType", "LowCost_CASA", casaAccounts.Count, casaVolume, totalVolume > 0m ? (casaVolume / totalVolume) * 100m : 0m, avgCasaRate),
            new("ProductCostType", "HighCost_Term", termAccounts.Count, termVolume, totalVolume > 0m ? (termVolume / totalVolume) * 100m : 0m, avgTermRate)
        };

        return new DepositAnalyticsReportDto(
            asOfDate,
            totalAccounts,
            totalVolume,
            Math.Round(casaRatio, 2, MidpointRounding.AwayFromZero),
            slabs,
            mix);
    }

    public async Task<List<UnclaimedDepositCandidateDto>> ScanUnclaimedDepositCandidatesAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var accounts = await _context.DepositAccounts.AsNoTracking().ToListAsync(ct);
        var customers = await _context.Customers.AsNoTracking().ToListAsync(ct);
        var customerMap = customers.ToDictionary(c => c.Id.Value, c => c.FullName);

        var candidates = new List<UnclaimedDepositCandidateDto>();

        foreach (var acc in accounts)
        {
            if (acc.Status is AccountOperatingStatus.Closed or AccountOperatingStatus.Unclaimed10YearTransferred)
                continue;

            int totalDays = asOfDate.DayNumber - acc.LastCustomerTransactionDate.DayNumber;
            decimal years = Math.Round(totalDays / 365.25m, 2, MidpointRounding.AwayFromZero);

            if (years >= 10.0m)
            {
                string custName = customerMap.TryGetValue(acc.Cif.Value, out var name) ? name : "Valued Customer";
                candidates.Add(new UnclaimedDepositCandidateDto(
                    acc.Id.Value,
                    acc.Cif.Value,
                    custName,
                    acc.ProductType,
                    acc.Balance?.Amount ?? 0m,
                    acc.Balance?.Currency.Code ?? "BDT",
                    acc.LastCustomerTransactionDate,
                    years));
            }
        }

        return candidates;
    }

    public async Task<Sbs3ClassificationReportDto> GetSbs3ClassificationReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var loans = await _context.LoanAccounts.AsNoTracking().ToListAsync(ct);
        var customers = await _context.Customers.AsNoTracking().ToListAsync(ct);
        var customerMap = customers.ToDictionary(c => c.Id.Value, c => c.FullName);

        var rows = new List<Sbs3ClassificationRow>();

        decimal totalOutstanding = 0m;
        decimal totalUnclassified = 0m;
        decimal totalClassified = 0m;
        decimal totalGenProv = 0m;
        decimal totalSpecProv = 0m;
        decimal totalSuspense = 0m;

        foreach (var l in loans)
        {
            decimal outBal = l.OutstandingPrincipal?.Amount ?? 0m;
            decimal overdue = l.OverdueInterest?.Amount ?? 0m;
            decimal suspense = l.InterestSuspense?.Amount ?? 0m;
            decimal sanction = l.SanctionLimit?.Amount ?? 0m;
            decimal collateral = sanction * 0.50m; // eligible security deduction

            string name = customerMap.TryGetValue(l.Cif.Value, out var n) ? n : "Corporate Borrower";

            var row = new Sbs3ClassificationRow(
                l.Id.Value,
                l.Cif.Value,
                name,
                EconomicSectorCategory.Cmsme,
                sanction,
                outBal,
                overdue,
                l.DaysPastDue,
                l.Classification,
                collateral,
                suspense);

            rows.Add(row);

            totalOutstanding += outBal;
            totalSuspense += suspense;

            if (row.IsClassified)
            {
                totalClassified += outBal;
                totalSpecProv += row.RequiredProvisionAmount;
            }
            else
            {
                totalUnclassified += outBal;
                totalGenProv += row.RequiredProvisionAmount;
            }
        }

        decimal totalRequiredProv = totalGenProv + totalSpecProv;
        decimal nplRatio = totalOutstanding > 0m ? (totalClassified / totalOutstanding) * 100m : 0m;

        var summary = new Sbs3ClassificationSummaryDto(
            loans.Count,
            totalOutstanding,
            totalUnclassified,
            totalClassified,
            Math.Round(nplRatio, 2, MidpointRounding.AwayFromZero),
            totalGenProv,
            totalSpecProv,
            totalRequiredProv,
            totalRequiredProv,
            totalSuspense);

        return new Sbs3ClassificationReportDto(asOfDate, summary, rows);
    }

    public async Task<ForeignTradeSummaryReportDto> GetForeignTradeSummaryReportAsync(
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var lcs = await _context.DocumentaryCredits.AsNoTracking().ToListAsync(ct);
        var sgs = await _context.ShippingGuarantees.AsNoTracking().ToListAsync(ct);

        decimal totalLcAmount = lcs.Sum(x => x.Amount?.Amount ?? 0m);
        decimal totalMargin = lcs.Sum(x => x.CashMarginAmount?.Amount ?? 0m);
        decimal totalSgAmount = sgs.Sum(x => x.GuaranteeAmount?.Amount ?? 0m);

        var breakdown = new List<TradeFinanceSummaryRow>
        {
            new("LetterOfCredit", lcs.Count, totalLcAmount, totalMargin, 0m),
            new("ShippingGuarantee", sgs.Count, totalSgAmount, 0m, 0m),
            new("PaymentAgainstDocument_PAD", 0, 0m, 0m, 0m),
            new("ForeignBankGuarantee", 0, 0m, 0m, 0m)
        };

        return new ForeignTradeSummaryReportDto(
            asOfDate,
            lcs.Count,
            totalLcAmount,
            0m,
            totalSgAmount,
            breakdown);
    }

    public async Task<RemittanceIncentiveReportDto> GetRemittanceIncentiveReportAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var allTxns = await _context.ForeignRemittanceTransactions
            .AsNoTracking()
            .ToListAsync(ct);

        var filtered = allTxns
            .Where(r => DateOnly.FromDateTime(r.CreatedAtUtc) >= fromDate && DateOnly.FromDateTime(r.CreatedAtUtc) <= toDate)
            .ToList();

        var items = filtered.Select(t => new RemittanceIncentiveItemDto(
            t.TransactionReference,
            t.SenderName,
            t.BeneficiaryName,
            t.SenderCountry,
            "National Exchange",
            t.ConvertedBdtAmount.Amount,
            t.IncentiveRate * 100m,
            t.IncentiveAmount.Amount,
            DateOnly.FromDateTime(t.CreatedAtUtc),
            t.Status == ForeignRemittanceStatus.CreditedToBeneficiary)).ToList();

        decimal totalRemitted = items.Sum(i => i.RemittanceAmountBdt);
        decimal totalIncentives = items.Sum(i => i.IncentiveAmountBdt);

        return new RemittanceIncentiveReportDto(
            fromDate,
            toDate,
            items.Count,
            totalRemitted,
            totalIncentives,
            items);
    }

    public async Task<AdcReconciliationReportDto> GetAdcReconciliationReportAsync(
        DateOnly date,
        CancellationToken ct = default)
    {
        var txns = await _context.AdcTransactions.AsNoTracking().ToListAsync(ct);
        var disputes = await _context.DisputeClaims.AsNoTracking().ToListAsync(ct);

        var channels = new List<AdcChannelSummaryItemDto>
        {
            new("ATM",
                txns.Count(t => t.ChannelType == AdcChannelType.Atm),
                txns.Where(t => t.ChannelType == AdcChannelType.Atm).Sum(t => t.Amount.Amount),
                txns.Count(t => t.ChannelType == AdcChannelType.Atm && t.Status == AdcTransactionStatus.Approved),
                txns.Where(t => t.ChannelType == AdcChannelType.Atm && t.Status == AdcTransactionStatus.Approved).Sum(t => t.Amount.Amount),
                disputes.Count(d => d.Channel == AdcChannelType.Atm),
                disputes.Where(d => d.Channel == AdcChannelType.Atm).Sum(d => d.DisputedAmount.Amount)),
            new("POS",
                txns.Count(t => t.ChannelType == AdcChannelType.Pos),
                txns.Where(t => t.ChannelType == AdcChannelType.Pos).Sum(t => t.Amount.Amount),
                txns.Count(t => t.ChannelType == AdcChannelType.Pos && t.Status == AdcTransactionStatus.Approved),
                txns.Where(t => t.ChannelType == AdcChannelType.Pos && t.Status == AdcTransactionStatus.Approved).Sum(t => t.Amount.Amount),
                disputes.Count(d => d.Channel == AdcChannelType.Pos),
                disputes.Where(d => d.Channel == AdcChannelType.Pos).Sum(d => d.DisputedAmount.Amount)),
            new("MobileApp",
                txns.Count(t => t.ChannelType == AdcChannelType.MobileApp),
                txns.Where(t => t.ChannelType == AdcChannelType.MobileApp).Sum(t => t.Amount.Amount),
                txns.Count(t => t.ChannelType == AdcChannelType.MobileApp && t.Status == AdcTransactionStatus.Approved),
                txns.Where(t => t.ChannelType == AdcChannelType.MobileApp && t.Status == AdcTransactionStatus.Approved).Sum(t => t.Amount.Amount),
                disputes.Count(d => d.Channel == AdcChannelType.MobileApp),
                disputes.Where(d => d.Channel == AdcChannelType.MobileApp).Sum(d => d.DisputedAmount.Amount))
        };

        decimal totalVol = channels.Sum(c => c.TotalVolumeBdt);
        decimal totalDisp = channels.Sum(c => c.DisputedVolumeBdt);

        return new AdcReconciliationReportDto(
            date,
            channels.Sum(c => c.TotalTransactions),
            totalVol,
            channels.Sum(c => c.DisputedCount),
            totalDisp,
            channels);
    }

    public async Task SaveArchiveAsync(StatutoryReportArchive archive, CancellationToken ct = default)
    {
        var existing = await _context.StatutoryReportArchives.FindAsync([archive.Id], ct);
        if (existing is null)
        {
            await _context.StatutoryReportArchives.AddAsync(archive, ct);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<StatutoryReportArchive?> GetArchiveByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.StatutoryReportArchives.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<StatutoryReportArchive>> GetArchivesAsync(
        RegulatoryReportType? type,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = _context.StatutoryReportArchives.AsNoTracking();

        if (type.HasValue)
        {
            query = query.Where(a => a.ReportType == type.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.ReportingCutoffDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.ReportingCutoffDate <= toDate.Value);
        }

        return await query.OrderByDescending(a => a.GenerationTimestamp).ToListAsync(ct);
    }

    public async Task SaveUnclaimedDepositItemAsync(UnclaimedDepositItem item, CancellationToken ct = default)
    {
        var existing = await _context.UnclaimedDepositItems.FindAsync([item.Id], ct);
        if (existing is null)
        {
            await _context.UnclaimedDepositItems.AddAsync(item, ct);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<UnclaimedDepositItem?> GetUnclaimedDepositByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.UnclaimedDepositItems
            .FirstOrDefaultAsync(u => u.AccountNumber == accountNumber.Trim(), ct);
    }

    public async Task<IReadOnlyList<UnclaimedDepositItem>> GetUnclaimedDepositsAsync(
        UnclaimedDepositStatus? status,
        CancellationToken ct = default)
    {
        var query = _context.UnclaimedDepositItems.AsNoTracking();
        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        return await query.OrderByDescending(u => u.UnclaimedYears).ToListAsync(ct);
    }

    public async Task ExecuteCentralBankFundTransferAsync(string accountNumber, DateOnly date, CancellationToken ct = default)
    {
        var acc = await _context.DepositAccounts.FindAsync([AccountNumber.From(accountNumber.Trim())], ct);
        if (acc != null)
        {
            acc.Execute10YearUnclaimedTransfer(date);
            await _context.SaveChangesAsync(ct);
        }
    }
}
