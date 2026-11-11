using CBS.Application.Shariah;
using CBS.Domain.Enums;
using CBS.Domain.Shariah;
using CBS.Domain.Shariah.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfShariahComplianceRepository : IShariahComplianceRepository
{
    private readonly CbsDbContext _context;

    public EfShariahComplianceRepository(CbsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MudarabaInvestmentPool?> GetPoolByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MudarabaInvestmentPools
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<MudarabaInvestmentPool>> GetAllPoolsAsync(CancellationToken ct = default)
    {
        return await _context.MudarabaInvestmentPools
            .OrderBy(p => p.PoolName)
            .ToListAsync(ct);
    }

    public async Task SavePoolAsync(MudarabaInvestmentPool pool, CancellationToken ct = default)
    {
        if (_context.Entry(pool).State == EntityState.Detached)
        {
            var exists = await _context.MudarabaInvestmentPools.AnyAsync(p => p.Id == pool.Id, ct);
            if (exists)
            {
                _context.MudarabaInvestmentPools.Update(pool);
            }
            else
            {
                await _context.MudarabaInvestmentPools.AddAsync(pool, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<AlWadiahAccount?> GetAlWadiahByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
    {
        return await _context.AlWadiahAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, ct);
    }

    public async Task<IReadOnlyList<AlWadiahAccount>> GetAlWadiahByCifAsync(string cif, CancellationToken ct = default)
    {
        return await _context.AlWadiahAccounts
            .Where(a => a.Cif == cif)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task SaveAlWadiahAccountAsync(AlWadiahAccount account, CancellationToken ct = default)
    {
        if (_context.Entry(account).State == EntityState.Detached)
        {
            var exists = await _context.AlWadiahAccounts.AnyAsync(a => a.Id == account.Id, ct);
            if (exists)
            {
                _context.AlWadiahAccounts.Update(account);
            }
            else
            {
                await _context.AlWadiahAccounts.AddAsync(account, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<MurabahaFinancingFacility?> GetMurabahaByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.MurabahaFinancingFacilities
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<MurabahaFinancingFacility?> GetMurabahaByFacilityNumberAsync(string facilityNumber, CancellationToken ct = default)
    {
        return await _context.MurabahaFinancingFacilities
            .FirstOrDefaultAsync(m => m.FacilityNumber == facilityNumber, ct);
    }

    public async Task<IReadOnlyList<MurabahaFinancingFacility>> GetMurabahaByCifAsync(string cif, CancellationToken ct = default)
    {
        return await _context.MurabahaFinancingFacilities
            .Where(m => m.Cif == cif)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task SaveMurabahaFacilityAsync(MurabahaFinancingFacility facility, CancellationToken ct = default)
    {
        if (_context.Entry(facility).State == EntityState.Detached)
        {
            var exists = await _context.MurabahaFinancingFacilities.AnyAsync(m => m.Id == facility.Id, ct);
            if (exists)
            {
                _context.MurabahaFinancingFacilities.Update(facility);
            }
            else
            {
                await _context.MurabahaFinancingFacilities.AddAsync(facility, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<QuardFinancingFacility?> GetQuardByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.QuardFinancingFacilities
            .FirstOrDefaultAsync(q => q.Id == id, ct);
    }

    public async Task<QuardFinancingFacility?> GetQuardByFacilityNumberAsync(string facilityNumber, CancellationToken ct = default)
    {
        return await _context.QuardFinancingFacilities
            .FirstOrDefaultAsync(q => q.FacilityNumber == facilityNumber, ct);
    }

    public async Task<IReadOnlyList<QuardFinancingFacility>> GetQuardByCifAsync(string cif, CancellationToken ct = default)
    {
        return await _context.QuardFinancingFacilities
            .Where(q => q.Cif == cif)
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task SaveQuardFacilityAsync(QuardFinancingFacility facility, CancellationToken ct = default)
    {
        if (_context.Entry(facility).State == EntityState.Detached)
        {
            var exists = await _context.QuardFinancingFacilities.AnyAsync(q => q.Id == facility.Id, ct);
            if (exists)
            {
                _context.QuardFinancingFacilities.Update(facility);
            }
            else
            {
                await _context.QuardFinancingFacilities.AddAsync(facility, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ShariahTaWidhCharityFund?> GetTaWidhByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ShariahTaWidhCharityFunds
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<ShariahTaWidhCharityFund>> GetAllTaWidhRecordsAsync(CancellationToken ct = default)
    {
        return await _context.ShariahTaWidhCharityFunds
            .OrderByDescending(t => t.AccruedAtUtc)
            .ToListAsync(ct);
    }

    public async Task SaveTaWidhRecordAsync(ShariahTaWidhCharityFund record, CancellationToken ct = default)
    {
        if (_context.Entry(record).State == EntityState.Detached)
        {
            var exists = await _context.ShariahTaWidhCharityFunds.AnyAsync(t => t.Id == record.Id, ct);
            if (exists)
            {
                _context.ShariahTaWidhCharityFunds.Update(record);
            }
            else
            {
                await _context.ShariahTaWidhCharityFunds.AddAsync(record, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task SeedDefaultIslamicPoolsIfEmptyAsync(CancellationToken ct = default)
    {
        if (await _context.MudarabaInvestmentPools.AnyAsync(ct))
            return;

        var defaultPool = new MudarabaInvestmentPool(
            "General Mudaraba Investment Pool (BDT)",
            MudarabaPoolType.GeneralInvestmentPool,
            "BDT",
            35.0m, // 35% Mudarib, 65% Rab-ul-Mal
            2.0m,  // 2% PER
            1.0m); // 1% IRR

        defaultPool.AddDeposit(15000000m); // Initial seed deposit
        defaultPool.SetWeightage(new MudarabaWeightage("MSB", "Mudaraba Savings Account", 1.00m, 5.50m));
        defaultPool.SetWeightage(new MudarabaWeightage("MTDR-03", "Mudaraba Term Deposit 3-Month", 1.15m, 6.75m));
        defaultPool.SetWeightage(new MudarabaWeightage("MTDR-12", "Mudaraba Term Deposit 12-Month", 1.35m, 7.50m));
        defaultPool.SetWeightage(new MudarabaWeightage("MSNTD", "Mudaraba Special Notice Term Deposit", 0.85m, 4.75m));

        await _context.MudarabaInvestmentPools.AddAsync(defaultPool, ct);
        await _context.SaveChangesAsync(ct);
    }
}
