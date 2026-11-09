using System.Text.RegularExpressions;

namespace CBS.Domain.ValueObjects;

/// <summary>
/// Harmonized System (HS) Code conforming to Bangladesh Bank and National Board of Revenue (NBR) Tariff schedule.
/// Invariants:
/// - Must be 6 or 8 digits (excluding formatting dots).
/// - Enforces Pre-Shipment Inspection (PSI) flags for mandatory chapters (e.g. 7207 iron/steel).
/// Reference: BRPD Circular 12, 5.1.1-vii, 5.1.1-xliv.
/// </summary>
public sealed record HsCode
{
    private static readonly Regex ValidHsRegex = new(@"^\d{4}\.?\d{2}(\.?\d{2})?$", RegexOptions.Compiled);

    public string Code { get; }
    public string NormalizedCode { get; }
    public bool RequiresPreShipmentInspection { get; }
    public bool IsRestrictedUnderIpo { get; }

    private HsCode(string code, string normalizedCode, bool requiresPsi, bool isRestricted)
    {
        Code = code;
        NormalizedCode = normalizedCode;
        RequiresPreShipmentInspection = requiresPsi;
        IsRestrictedUnderIpo = isRestricted;
    }

    public static HsCode Create(string code, bool requiresPsiOverride = false, bool isRestricted = false)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("HS Code cannot be empty.", nameof(code));
        }

        var trimmed = code.Trim();
        if (!ValidHsRegex.IsMatch(trimmed))
        {
            throw new ArgumentException($"Invalid HS Code format: '{trimmed}'. Must be 6 or 8 digits (e.g. 7207.11.00).", nameof(code));
        }

        var normalized = trimmed.Replace(".", "");

        // BRPD 5.1.1-xliv: HS Code 7207 and steel scrap products mandatorily require Pre-Shipment Inspection (PSI)
        var requiresPsi = requiresPsiOverride || normalized.StartsWith("7207") || normalized.StartsWith("7204");

        return new HsCode(trimmed, normalized, requiresPsi, isRestricted);
    }

    public override string ToString() => Code;
}
