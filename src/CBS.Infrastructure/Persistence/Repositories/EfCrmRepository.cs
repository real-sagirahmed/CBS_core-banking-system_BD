using CBS.Application.Customers;
using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfCrmRepository : ICrmRepository
{
    private readonly CbsDbContext _dbContext;

    public EfCrmRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SpecimenSignature?> GetSignatureByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SpecimenSignatures
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SpecimenSignature>> GetSignaturesByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SpecimenSignatures
            .Where(s => s.CustomerCif == cif)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddSignatureAsync(SpecimenSignature signature, CancellationToken cancellationToken = default)
    {
        await _dbContext.SpecimenSignatures.AddAsync(signature, cancellationToken);
    }

    public Task UpdateSignatureAsync(SpecimenSignature signature, CancellationToken cancellationToken = default)
    {
        _dbContext.SpecimenSignatures.Update(signature);
        return Task.CompletedTask;
    }

    public async Task<AccountSignatoryMandate?> GetMandateByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AccountSignatoryMandates
            .FirstOrDefaultAsync(m => m.AccountNumber == accountNumber && m.IsActive, cancellationToken);
    }

    public async Task AddMandateAsync(AccountSignatoryMandate mandate, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountSignatoryMandates.AddAsync(mandate, cancellationToken);
    }

    public Task UpdateMandateAsync(AccountSignatoryMandate mandate, CancellationToken cancellationToken = default)
    {
        _dbContext.AccountSignatoryMandates.Update(mandate);
        return Task.CompletedTask;
    }

    public async Task<CorporateCustomerEntity?> GetCorporateEntityByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CorporateCustomerEntities
            .FirstOrDefaultAsync(c => c.Id == cif, cancellationToken);
    }

    public async Task<CorporateCustomerEntity?> GetCorporateEntityByRegistrationNumberAsync(string registrationNumber, CancellationToken cancellationToken = default)
    {
        string normalized = registrationNumber.Trim().ToUpperInvariant();
        return await _dbContext.CorporateCustomerEntities
            .FirstOrDefaultAsync(c => c.RegistrationNumber == normalized, cancellationToken);
    }

    public async Task AddCorporateEntityAsync(CorporateCustomerEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.CorporateCustomerEntities.AddAsync(entity, cancellationToken);
    }

    public Task UpdateCorporateEntityAsync(CorporateCustomerEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.CorporateCustomerEntities.Update(entity);
        return Task.CompletedTask;
    }

    public async Task<CorporateGroup?> GetCorporateGroupByIdAsync(string groupId, CancellationToken cancellationToken = default)
    {
        string normalized = groupId.Trim().ToUpperInvariant();
        return await _dbContext.CorporateGroups
            .FirstOrDefaultAsync(g => g.Id == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<CorporateGroup>> GetAllCorporateGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.CorporateGroups
            .OrderBy(g => g.GroupName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddCorporateGroupAsync(CorporateGroup group, CancellationToken cancellationToken = default)
    {
        await _dbContext.CorporateGroups.AddAsync(group, cancellationToken);
    }

    public Task UpdateCorporateGroupAsync(CorporateGroup group, CancellationToken cancellationToken = default)
    {
        _dbContext.CorporateGroups.Update(group);
        return Task.CompletedTask;
    }

    public async Task<CustomerDocumentRecord?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerDocumentRecords
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDocumentRecord>> GetDocumentsByCifAsync(CifNumber cif, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerDocumentRecords
            .Where(d => d.CustomerCif == cif)
            .OrderByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerDocumentRecord>> GetExpiringDocumentsAsync(DateOnly asOfDate, int thresholdDays, CancellationToken cancellationToken = default)
    {
        var cutoff = asOfDate.AddDays(thresholdDays);
        return await _dbContext.CustomerDocumentRecords
            .Where(d => d.ExpiryDate != null && d.ExpiryDate <= cutoff)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default)
    {
        await _dbContext.CustomerDocumentRecords.AddAsync(document, cancellationToken);
    }

    public Task UpdateDocumentAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default)
    {
        _dbContext.CustomerDocumentRecords.Update(document);
        return Task.CompletedTask;
    }
}
