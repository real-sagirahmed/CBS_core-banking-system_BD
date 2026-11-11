namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Cryptographic Test Key Engine for Demand Draft (DD)
// and Telegraphic/Telephonic Transfer (TT) conforming to
// Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3.12.1-iii and 3.13.1-iii.
public static class CryptographicTestKeyService
{
    private const int SecretBankBaseKey = 42197;

    public static string GenerateTestKey(
        DateOnly issueDate,
        decimal amount,
        string issuingSol,
        string payeeSol)
    {
        int dateCode = (issueDate.Year % 100) * 372 + (issueDate.Month * 31) + issueDate.Day;

        // Amount slab weight
        int amountWeight = amount switch
        {
            <= 10_000m => 101,
            <= 50_000m => 203,
            <= 100_000m => 307,
            <= 500_000m => 409,
            <= 1_000_000m => 521,
            _ => 643
        };

        int issCode = int.TryParse(issuingSol, out var i) ? i : 101;
        int payCode = int.TryParse(payeeSol, out var p) ? p : 202;

        long rawScore = SecretBankBaseKey + dateCode + amountWeight + (issCode * 17L) + (payCode * 23L) + (long)amount % 1000;
        int testKey = (int)(Math.Abs(rawScore) % 100000);

        return testKey.ToString("D5");
    }

    public static bool VerifyTestKey(
        string providedTestKey,
        DateOnly issueDate,
        decimal amount,
        string issuingSol,
        string payeeSol)
    {
        string expectedTestKey = GenerateTestKey(issueDate, amount, issuingSol, payeeSol);
        return string.Equals(providedTestKey?.Trim(), expectedTestKey, StringComparison.Ordinal);
    }
}
