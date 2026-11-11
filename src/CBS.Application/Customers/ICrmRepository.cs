using CBS.Domain.Customers;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers;

public interface ICrmRepository
{
    // Specimen Signatures
    Task<SpecimenSignature?> GetSignatureByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpecimenSignature>> GetSignaturesByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task AddSignatureAsync(SpecimenSignature signature, CancellationToken cancellationToken = default);
    Task UpdateSignatureAsync(SpecimenSignature signature, CancellationToken cancellationToken = default);

    // Account Signatory Mandates
    Task<AccountSignatoryMandate?> GetMandateByAccountNumberAsync(AccountNumber accountNumber, CancellationToken cancellationToken = default);
    Task AddMandateAsync(AccountSignatoryMandate mandate, CancellationToken cancellationToken = default);
    Task UpdateMandateAsync(AccountSignatoryMandate mandate, CancellationToken cancellationToken = default);

    // Corporate Customer Entities
    Task<CorporateCustomerEntity?> GetCorporateEntityByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<CorporateCustomerEntity?> GetCorporateEntityByRegistrationNumberAsync(string registrationNumber, CancellationToken cancellationToken = default);
    Task AddCorporateEntityAsync(CorporateCustomerEntity entity, CancellationToken cancellationToken = default);
    Task UpdateCorporateEntityAsync(CorporateCustomerEntity entity, CancellationToken cancellationToken = default);

    // Corporate Groups
    Task<CorporateGroup?> GetCorporateGroupByIdAsync(string groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorporateGroup>> GetAllCorporateGroupsAsync(CancellationToken cancellationToken = default);
    Task AddCorporateGroupAsync(CorporateGroup group, CancellationToken cancellationToken = default);
    Task UpdateCorporateGroupAsync(CorporateGroup group, CancellationToken cancellationToken = default);

    // Customer Documents
    Task<CustomerDocumentRecord?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDocumentRecord>> GetDocumentsByCifAsync(CifNumber cif, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerDocumentRecord>> GetExpiringDocumentsAsync(DateOnly asOfDate, int thresholdDays, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(CustomerDocumentRecord document, CancellationToken cancellationToken = default);
}
