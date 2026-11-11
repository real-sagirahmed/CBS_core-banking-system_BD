using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Customer KYC & Operational Document Lifecycle,
// Document Verification, and Expiry Alert Engine (BRPD Circular 12/2024 Module 8.1.3, 8.1.8, 8.1.18-h).
public class CustomerDocumentRecord : AggregateRoot<Guid>
{
    public CifNumber CustomerCif { get; private set; }
    public CrmDocumentCategory Category { get; private set; }
    public string DocumentNumber { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string IssuingAuthority { get; private set; }
    public bool IsVerified { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected CustomerDocumentRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public CustomerDocumentRecord(
        Guid id,
        CifNumber customerCif,
        CrmDocumentCategory category,
        string documentNumber,
        DateOnly issueDate,
        DateOnly? expiryDate,
        string issuingAuthority,
        DateTime uploadedAtUtc) : base(id)
    {
        ArgumentNullException.ThrowIfNull(customerCif, nameof(customerCif));
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new ArgumentException("Document number is required.", nameof(documentNumber));

        CustomerCif = customerCif;
        Category = category;
        DocumentNumber = documentNumber.Trim().ToUpperInvariant();
        IssueDate = issueDate;
        ExpiryDate = expiryDate;
        IssuingAuthority = string.IsNullOrWhiteSpace(issuingAuthority) ? "Government Authority" : issuingAuthority.Trim();
        UploadedAtUtc = uploadedAtUtc;
        IsVerified = false;
    }

    public DocumentExpiryStatus CheckExpiryStatus(DateOnly asOfDate)
    {
        if (!ExpiryDate.HasValue)
            return DocumentExpiryStatus.Valid;

        if (ExpiryDate.Value < asOfDate)
            return DocumentExpiryStatus.Expired;

        if (ExpiryDate.Value <= asOfDate.AddDays(30))
            return DocumentExpiryStatus.ExpiringSoon;

        return DocumentExpiryStatus.Valid;
    }

    public void Verify(string checkerUserId, DateTime verifiedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        IsVerified = true;
        VerifiedByUserId = checkerUserId.Trim();
        VerifiedAtUtc = verifiedAtUtc;
    }
}
