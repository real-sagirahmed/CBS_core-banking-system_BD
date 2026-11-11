using CBS.Application.Specialized;
using CBS.Domain.BranchCash;
using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.Lending.Agricultural;
using CBS.Domain.Lending.Discounted;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfSpecializedBankingRepository : ISpecializedBankingRepository
{
    private readonly CbsDbContext _dbContext;

    public EfSpecializedBankingRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // 1. Agricultural Credit
    public async Task<AgriculturalLoanFacility?> GetAgriLoanAsync(AccountNumber accountNumber, CancellationToken ct = default)
    {
        return await _dbContext.AgriculturalLoans.FirstOrDefaultAsync(a => a.Id == accountNumber, ct);
    }

    public async Task AddAgriLoanAsync(AgriculturalLoanFacility facility, CancellationToken ct = default)
    {
        await _dbContext.AgriculturalLoans.AddAsync(facility, ct);
    }

    public async Task<IReadOnlyList<AgriculturalLoanFacility>> ListAgriLoansByCifAsync(CifNumber cif, CancellationToken ct = default)
    {
        return await _dbContext.AgriculturalLoans.Where(a => a.Cif == cif).ToListAsync(ct);
    }

    // 2. Bill Discounting
    public async Task<BillDiscountingFacility?> GetBillDiscountingAsync(AccountNumber accountNumber, CancellationToken ct = default)
    {
        return await _dbContext.DiscountedBills.FirstOrDefaultAsync(b => b.Id == accountNumber, ct);
    }

    public async Task<BillDiscountingFacility?> GetBillByReferenceAsync(string billReference, CancellationToken ct = default)
    {
        return await _dbContext.DiscountedBills.FirstOrDefaultAsync(b => b.BillReferenceNumber == billReference, ct);
    }

    public async Task AddBillDiscountingAsync(BillDiscountingFacility facility, CancellationToken ct = default)
    {
        await _dbContext.DiscountedBills.AddAsync(facility, ct);
    }

    // 3. Deposit Multiplication Schemes
    public async Task<DepositMultiplicationAccount?> GetMultiplicationAccountAsync(AccountNumber accountNumber, CancellationToken ct = default)
    {
        return await _dbContext.DepositMultiplicationAccounts.FirstOrDefaultAsync(m => m.Id == accountNumber, ct);
    }

    public async Task AddMultiplicationAccountAsync(DepositMultiplicationAccount account, CancellationToken ct = default)
    {
        await _dbContext.DepositMultiplicationAccounts.AddAsync(account, ct);
    }

    // 4. Inter-Branch Payment Instruments
    public async Task<InterBranchPaymentInstrument?> GetInstrumentByNumberAsync(string instrumentNumber, CancellationToken ct = default)
    {
        return await _dbContext.InterBranchPaymentInstruments.FirstOrDefaultAsync(i => i.InstrumentNumber == instrumentNumber, ct);
    }

    public async Task AddInstrumentAsync(InterBranchPaymentInstrument instrument, CancellationToken ct = default)
    {
        await _dbContext.InterBranchPaymentInstruments.AddAsync(instrument, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _dbContext.SaveChangesAsync(ct);
    }
}
