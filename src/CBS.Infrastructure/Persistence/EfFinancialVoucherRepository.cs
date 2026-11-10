using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.GeneralLedger;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfFinancialVoucherRepository : IFinancialVoucherRepository
{
    private readonly CbsDbContext _dbContext;

    public EfFinancialVoucherRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(FinancialVoucher voucher, CancellationToken cancellationToken = default)
    {
        await _dbContext.FinancialVouchers.AddAsync(voucher, cancellationToken);
    }

    public async Task<FinancialVoucher?> GetByVoucherNumberAsync(string voucherNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FinancialVouchers
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.VoucherNumber == voucherNumber, cancellationToken);
    }

    public async Task<FinancialVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FinancialVouchers
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialVoucher>> GetByDateRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string? branchSol = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FinancialVouchers
            .Include(v => v.Entries)
            .Where(v => v.ValueDate >= fromDate && v.ValueDate <= toDate);

        if (!string.IsNullOrWhiteSpace(branchSol))
        {
            query = query.Where(v => v.BranchSol == branchSol);
        }

        return await query
            .OrderBy(v => v.ValueDate)
            .ThenBy(v => v.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrialBalanceDto> GenerateTrialBalanceAsync(
        DateOnly asOfDate,
        string? branchSol = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.FinancialVouchers
            .Include(v => v.Entries)
            .Where(v => v.Status == VoucherStatus.Posted && v.ValueDate <= asOfDate);

        if (!string.IsNullOrWhiteSpace(branchSol))
        {
            query = query.Where(v => v.BranchSol == branchSol);
        }

        var vouchers = await query.ToListAsync(cancellationToken);
        var coaList = await _dbContext.ChartOfAccounts.ToListAsync(cancellationToken);
        var coaMap = coaList.ToDictionary(c => c.Id, c => c);

        var lines = new List<TrialBalanceLineDto>();
        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        var entriesGrouped = vouchers
            .SelectMany(v => v.Entries)
            .GroupBy(e => e.AccountCode)
            .OrderBy(g => g.Key);

        foreach (var grp in entriesGrouped)
        {
            string code = grp.Key;
            decimal debitSum = grp.Where(e => e.Type == DebitCredit.Debit).Sum(e => e.BaseAmount.Amount);
            decimal creditSum = grp.Where(e => e.Type == DebitCredit.Credit).Sum(e => e.BaseAmount.Amount);

            totalDebits += debitSum;
            totalCredits += creditSum;

            string accountName = $"GL Account {code}";
            AccountCategory category = AccountCategory.Asset;

            if (coaMap.TryGetValue(code, out var coa))
            {
                accountName = coa.Name;
                category = coa.Category;
            }
            else
            {
                category = code.FirstOrDefault() switch
                {
                    '1' => AccountCategory.Asset,
                    '2' => AccountCategory.Liability,
                    '3' => AccountCategory.Equity,
                    '4' => AccountCategory.Income,
                    '5' => AccountCategory.Expense,
                    _ => AccountCategory.Asset
                };
            }

            bool isDebitNormal = category is AccountCategory.Asset or AccountCategory.Expense;
            decimal netBalance = isDebitNormal ? (debitSum - creditSum) : (creditSum - debitSum);
            string normalBalance = isDebitNormal ? "Debit" : "Credit";

            lines.Add(new TrialBalanceLineDto(
                code,
                accountName,
                category,
                debitSum,
                creditSum,
                netBalance,
                normalBalance));
        }

        decimal discrepancy = Math.Abs(totalDebits - totalCredits);
        bool isBalanced = discrepancy <= 0.00001m;

        return new TrialBalanceDto(
            asOfDate,
            branchSol,
            lines,
            totalDebits,
            totalCredits,
            discrepancy,
            isBalanced);
    }
}
