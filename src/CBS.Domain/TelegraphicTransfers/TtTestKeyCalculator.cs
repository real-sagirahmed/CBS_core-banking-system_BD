// ARCHITECTURAL INTENT: Cryptographic TT Test Number Calculator and Authenticator
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13.1-iii, 3.13.2-ii).

using System;
using System.Security.Cryptography;
using System.Text;

namespace CBS.Domain.TelegraphicTransfers;

public static class TtTestKeyCalculator
{
    private const string Salt = "BRPD-TELEGRAPHIC-TRANSFER-SECRET-SEED-2024";

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
        var num = BitConverter.ToUInt32(hash, 0) % 900_000 + 100_000;
        return $"TT-TEST-{num:D6}";
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
