// ARCHITECTURAL INTENT: Cryptographic Test Key Generation and Verification Engine for Demand Drafts
// adhering to inter-branch settlement rules in Bangladesh Bank BRPD Circular No. 12 (3.12.1-iii).

using System;
using System.Security.Cryptography;
using System.Text;

namespace CBS.Domain.PaymentInstruments;

public static class DdTestKeyCalculator
{
    private const string Salt = "BRPD-DD-SECRET-KEY-SEED-2024";

    public static string GenerateTestKey(
        DateOnly issueDate,
        string issuingBranchCode,
        string draweeBranchCode,
        decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuingBranchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(draweeBranchCode);
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        var raw = $"{issueDate:yyyyMMdd}|{issuingBranchCode.Trim().ToUpperInvariant()}|{draweeBranchCode.Trim().ToUpperInvariant()}|{amount:F2}|{Salt}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var num = BitConverter.ToUInt32(hash, 0) % 900_000 + 100_000; // 6-digit numeric test number
        return $"TK-{num:D6}";
    }

    public static bool VerifyTestKey(
        string testKey,
        DateOnly issueDate,
        string issuingBranchCode,
        string draweeBranchCode,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(testKey)) return false;
        var expected = GenerateTestKey(issueDate, issuingBranchCode, draweeBranchCode, amount);
        return string.Equals(testKey.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
