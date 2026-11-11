using System.Security.Cryptography;
using System.Text;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Specimen Signatures, Thumbprints, and Biometric Stamps must be stored
// in AES-256 encrypted and compressed format with SHA-256 integrity checksums,
// tracking full audit history (BRPD Circular 12/2024 Module 8.1.12-b, c, f, g, h).
public class SpecimenSignature : AggregateRoot<Guid>
{
    public CifNumber CustomerCif { get; private set; }
    public string SignatoryName { get; private set; }
    public string Designation { get; private set; }
    public string NationalIdOrPassport { get; private set; }
    public SignatureType Type { get; private set; }
    public string EncryptedPayloadBase64 { get; private set; }
    public string IntegrityChecksumSha256 { get; private set; }
    public SignatureStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; }
    public string WorkstationIp { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevocationReason { get; private set; }

    #pragma warning disable CS8618
    protected SpecimenSignature() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public SpecimenSignature(
        Guid id,
        CifNumber customerCif,
        string signatoryName,
        string designation,
        string nationalIdOrPassport,
        SignatureType type,
        string rawSignatureDataOrBase64,
        string createdByUserId,
        string workstationIp,
        DateTime createdAtUtc,
        byte[] encryptionKeyAes256) : base(id)
    {
        ArgumentNullException.ThrowIfNull(customerCif, nameof(customerCif));
        if (string.IsNullOrWhiteSpace(signatoryName))
            throw new ArgumentException("Signatory name is required.", nameof(signatoryName));
        if (string.IsNullOrWhiteSpace(nationalIdOrPassport))
            throw new ArgumentException("National ID or Passport is required.", nameof(nationalIdOrPassport));
        if (string.IsNullOrWhiteSpace(rawSignatureDataOrBase64))
            throw new ArgumentException("Raw signature data is required.", nameof(rawSignatureDataOrBase64));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required.", nameof(createdByUserId));
        if (encryptionKeyAes256 is null || encryptionKeyAes256.Length != 32)
            throw new ArgumentException("Valid 256-bit AES encryption key is strictly required.", nameof(encryptionKeyAes256));

        CustomerCif = customerCif;
        SignatoryName = signatoryName.Trim();
        Designation = string.IsNullOrWhiteSpace(designation) ? "Authorized Signatory" : designation.Trim();
        NationalIdOrPassport = nationalIdOrPassport.Trim().ToUpperInvariant();
        Type = type;
        CreatedByUserId = createdByUserId.Trim();
        WorkstationIp = string.IsNullOrWhiteSpace(workstationIp) ? "127.0.0.1" : workstationIp.Trim();
        CreatedAtUtc = createdAtUtc;
        Status = SignatureStatus.Active;

        // SHA-256 integrity checksum
        byte[] rawBytes = Encoding.UTF8.GetBytes(rawSignatureDataOrBase64);
        IntegrityChecksumSha256 = Convert.ToHexString(SHA256.HashData(rawBytes));

        // Encrypt with AES-256-CBC
        EncryptedPayloadBase64 = EncryptAes256(rawBytes, encryptionKeyAes256);
    }

    public void Revoke(string reason, string revokedByUserId, DateTime revokedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Revocation reason is required.", nameof(reason));

        if (Status != SignatureStatus.Active)
        {
            throw new InvariantViolationException(
                "InvalidSignatureStatusForRevocation",
                "BRPD Circular 12/2024 (8.1.12)",
                $"Cannot revoke specimen signature in status '{Status}'.");
        }

        Status = SignatureStatus.Revoked;
        RevocationReason = reason.Trim();
        RevokedAtUtc = revokedAtUtc;
    }

    private static string EncryptAes256(byte[] plainBytes, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to ciphertext
        byte[] combined = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, combined, aes.IV.Length, encrypted.Length);

        return Convert.ToBase64String(combined);
    }

    public static string DecryptAes256(string base64Combined, byte[] key)
    {
        byte[] combined = Convert.FromBase64String(base64Combined);
        using var aes = Aes.Create();
        aes.Key = key;

        byte[] iv = new byte[16];
        byte[] ciphertext = new byte[combined.Length - 16];
        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, ciphertext, 0, ciphertext.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        byte[] decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
