using System.Text.RegularExpressions;

namespace CBS.Domain.AiIntelligence;

/// <summary>
/// Client-Side Deterministic PII Sanitizer and Redaction Engine conforming to Bangladesh Bank BRPD Circular 12 (Module 9.1.1-xxv, 10.1.3).
/// </summary>
public static class PiiSanitizer
{
    // ARCHITECTURAL INTENT: Client-Side Deterministic PII Sanitizer Locked (BRPD 9.1.1-xxv, 10.1.3).
    // Raw unmasked customer PII (Bangladeshi NID, e-TIN, 13-digit CBS Account Number, phone numbers)
    // is strictly prohibited from egressing towards external or foundational model APIs.
    // Redaction must be deterministic, non-reversible in context, and preserve trailing identifiers for officer auditability.

    private static readonly Regex PhoneRegex = new(@"(?:\+?880|0)1[3-9]\d{8}\b", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    // Context-sensitive prefixes
    private static readonly Regex AccountPrefixedRegex = new(@"(?i:\b(?:account|acct|a/c|a/c\s+no|acc|savings|current|loan)\b[\s:#-]*)(\d{13})\b", RegexOptions.Compiled);
    private static readonly Regex NidPrefixedRegex = new(@"(?i:\b(?:nid|national\s+id|smart\s+nid|smart\s+card)\b[\s:#-]*)(\d{10}|\d{13}|\d{17})\b", RegexOptions.Compiled);
    private static readonly Regex TinPrefixedRegex = new(@"(?i:\b(?:tin|e-tin|tax\s+id)\b[\s:#-]*)(\d{12})\b", RegexOptions.Compiled);

    // Standalone structural digit patterns
    private static readonly Regex Standalone17DigitNidRegex = new(@"\b\d{17}\b", RegexOptions.Compiled);
    private static readonly Regex Standalone12DigitTinRegex = new(@"\b\d{12}\b", RegexOptions.Compiled);
    private static readonly Regex Standalone10DigitNidRegex = new(@"\b\d{10}\b", RegexOptions.Compiled);
    private static readonly Regex Standalone13DigitRegex = new(@"\b\d{13}\b", RegexOptions.Compiled);

    // Payment Cards (16 digits) and Passports (PCI-DSS & BRPD 9.1.1-xxv)
    private static readonly Regex CardPanHyphenRegex = new(@"\b\d{4}[-\s]\d{4}[-\s]\d{4}[-\s]\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex Standalone16DigitCardRegex = new(@"\b\d{16}\b", RegexOptions.Compiled);
    private static readonly Regex PassportPrefixedRegex = new(@"(?i:\b(?:passport|pass\s*no|pp)\b[\s:#-]*)([A-Za-z]{1,2}\d{7,8})\b", RegexOptions.Compiled);

    public static string SanitizePrompt(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var result = input;

        // 1. Mask Phone Numbers (+880-***-XXXX)
        result = PhoneRegex.Replace(result, m =>
        {
            var val = m.Value;
            var last4 = val[^4..];
            return $"+880-***-{last4}";
        });

        // 2. Mask Emails (a***@domain.com)
        result = EmailRegex.Replace(result, m =>
        {
            var val = m.Value;
            var atIndex = val.IndexOf('@');
            if (atIndex <= 1) return "***" + val[atIndex..];
            return val[0] + "***" + val[atIndex..];
        });

        // 3. Mask Context-Prefixed Account Numbers
        result = AccountPrefixedRegex.Replace(result, m =>
        {
            var digits = m.Groups[1].Value;
            var prefix = m.Value[..^digits.Length];
            return $"{prefix}ACCT-***-{digits[^4..]}";
        });

        // 4. Mask Context-Prefixed NID Numbers
        result = NidPrefixedRegex.Replace(result, m =>
        {
            var digits = m.Groups[1].Value;
            var prefix = m.Value[..^digits.Length];
            return $"{prefix}NID-***-{digits[^4..]}";
        });

        // 5. Mask Context-Prefixed TIN Numbers
        result = TinPrefixedRegex.Replace(result, m =>
        {
            var digits = m.Groups[1].Value;
            var prefix = m.Value[..^digits.Length];
            return $"{prefix}TIN-***-{digits[^4..]}";
        });

        // 6. Mask Standalone 17-digit NID numbers (NID-***-XXXX)
        result = Standalone17DigitNidRegex.Replace(result, m => $"NID-***-{m.Value[^4..]}");

        // 7. Mask Standalone 12-digit e-TIN numbers (TIN-***-XXXX)
        result = Standalone12DigitTinRegex.Replace(result, m => $"TIN-***-{m.Value[^4..]}");

        // 8. Mask Standalone 10-digit Smart NID numbers (NID-***-XXXX)
        result = Standalone10DigitNidRegex.Replace(result, m => $"NID-***-{m.Value[^4..]}");

        // 9. Remaining Standalone 13-digit numbers are CBS Account Numbers (ACCT-***-XXXX)
        result = Standalone13DigitRegex.Replace(result, m => $"ACCT-***-{m.Value[^4..]}");

        // 10. Mask 16-digit Payment Card PANs (with hyphens/spaces: CARD-***-XXXX)
        result = CardPanHyphenRegex.Replace(result, m => $"CARD-***-{m.Value.Replace("-", "").Replace(" ", "")[^4..]}");

        // 11. Mask Standalone 16-digit Card PANs (CARD-***-XXXX)
        result = Standalone16DigitCardRegex.Replace(result, m => $"CARD-***-{m.Value[^4..]}");

        // 12. Mask Context-Prefixed Passport Numbers (PASS-***-XXXX)
        result = PassportPrefixedRegex.Replace(result, m =>
        {
            var docId = m.Groups[1].Value;
            var prefix = m.Value[..^docId.Length];
            return $"{prefix}PASS-***-{docId[^4..]}";
        });

        return result;
    }

    public static bool ContainsRawPii(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return PhoneRegex.IsMatch(input) ||
               Standalone17DigitNidRegex.IsMatch(input) ||
               Standalone12DigitTinRegex.IsMatch(input) ||
               Standalone10DigitNidRegex.IsMatch(input) ||
               Standalone13DigitRegex.IsMatch(input) ||
               CardPanHyphenRegex.IsMatch(input) ||
               Standalone16DigitCardRegex.IsMatch(input) ||
               PassportPrefixedRegex.IsMatch(input);
    }
}
