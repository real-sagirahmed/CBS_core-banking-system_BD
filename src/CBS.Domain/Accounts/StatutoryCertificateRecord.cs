using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Accounts;

// ARCHITECTURAL INTENT: Official Audit Archive for Statutory Balance Confirmation Letters,
// NBR Section 102/53F Withholding Tax Certificates, and Statement Copies (BRPD 3.1.17-iii, 3.1.19-i, ii).
public class StatutoryCertificateRecord : AggregateRoot<Guid>
{
    public string CertificateNumber { get; private set; }
    public string AccountNumber { get; private set; }
    public string CifNumber { get; private set; }
    public CertificateType Type { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public string IssuedByUserId { get; private set; }
    public Money FeeCharged { get; private set; }
    public Money VatCharged { get; private set; }
    public string DetailsJson { get; private set; }

    #pragma warning disable CS8618
    protected StatutoryCertificateRecord() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public StatutoryCertificateRecord(
        Guid id,
        string certificateNumber,
        string accountNumber,
        string cifNumber,
        CertificateType type,
        DateTime issuedAtUtc,
        string issuedByUserId,
        Money feeCharged,
        Money vatCharged,
        string detailsJson) : base(id)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber))
            throw new ArgumentException("Certificate number is required.", nameof(certificateNumber));
        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(cifNumber))
            throw new ArgumentException("CIF number is required.", nameof(cifNumber));
        if (string.IsNullOrWhiteSpace(issuedByUserId))
            throw new ArgumentException("Issuer user ID is required.", nameof(issuedByUserId));

        ArgumentNullException.ThrowIfNull(feeCharged, nameof(feeCharged));
        ArgumentNullException.ThrowIfNull(vatCharged, nameof(vatCharged));

        CertificateNumber = certificateNumber.Trim().ToUpperInvariant();
        AccountNumber = accountNumber.Trim();
        CifNumber = cifNumber.Trim();
        Type = type;
        IssuedAtUtc = issuedAtUtc;
        IssuedByUserId = issuedByUserId.Trim();
        FeeCharged = feeCharged;
        VatCharged = vatCharged;
        DetailsJson = detailsJson ?? string.Empty;
    }
}
