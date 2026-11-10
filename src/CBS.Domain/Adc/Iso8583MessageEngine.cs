using System.Text;

namespace CBS.Domain.Adc;

/// <summary>
/// Structured representation of an ISO 8583 Financial Message.
/// Standardized for Core Banking ATM / POS / NPSB / Switching integrations (BRPD 7.2.1-ii).
/// </summary>
public sealed class Iso8583Message
{
    public string Mti { get; set; } = string.Empty;
    public Dictionary<int, string> Fields { get; } = new();

    public string? Pan => Fields.GetValueOrDefault(2);
    public string? ProcessingCode => Fields.GetValueOrDefault(3);
    public string? Amount => Fields.GetValueOrDefault(4);
    public string? Stan => Fields.GetValueOrDefault(11);
    public string? LocalTime => Fields.GetValueOrDefault(12);
    public string? LocalDate => Fields.GetValueOrDefault(13);
    public string? Rrn => Fields.GetValueOrDefault(37);
    public string? ResponseCode => Fields.GetValueOrDefault(39);
    public string? TerminalId => Fields.GetValueOrDefault(41);
    public string? CurrencyCode => Fields.GetValueOrDefault(49);
    public string? AccountNumber => Fields.GetValueOrDefault(102);

    public decimal GetAmountDecimal()
    {
        if (string.IsNullOrWhiteSpace(Amount) || !long.TryParse(Amount, out var rawAmount))
            return 0m;

        return rawAmount / 100m;
    }

    public void SetAmountDecimal(decimal amount)
    {
        long poisha = (long)Math.Round(amount * 100m);
        Fields[4] = poisha.ToString().PadLeft(12, '0');
    }
}

/// <summary>
/// Parser, Builder and Validator for ISO 8583 Financial Messages.
/// </summary>
public static class Iso8583MessageEngine
{
    // ARCHITECTURAL INTENT: ISO 8583 Financial Transaction Standard (BRPD 7.2.1-ii).
    // Provides standard message encoding/decoding for ATM switches, POS terminals, and NPSB rails.

    public static Iso8583Message BuildResponse(Iso8583Message request, string responseCode)
    {
        var response = new Iso8583Message();

        // 0200 -> 0210 (Financial Response), 0420 -> 0430 (Reversal Response)
        response.Mti = request.Mti switch
        {
            "0200" => "0210",
            "0420" => "0430",
            "0100" => "0110",
            _ => "0210"
        };

        // Echo required identification fields
        if (request.Pan != null) response.Fields[2] = request.Pan;
        if (request.ProcessingCode != null) response.Fields[3] = request.ProcessingCode;
        if (request.Amount != null) response.Fields[4] = request.Amount;
        if (request.Stan != null) response.Fields[11] = request.Stan;
        if (request.LocalTime != null) response.Fields[12] = request.LocalTime;
        if (request.LocalDate != null) response.Fields[13] = request.LocalDate;
        if (request.Rrn != null) response.Fields[37] = request.Rrn;
        if (request.TerminalId != null) response.Fields[41] = request.TerminalId;
        if (request.CurrencyCode != null) response.Fields[49] = request.CurrencyCode;
        if (request.AccountNumber != null) response.Fields[102] = request.AccountNumber;

        // Set Response Code (DE 39)
        response.Fields[39] = responseCode.PadLeft(2, '0');

        return response;
    }

    public static string Pack(Iso8583Message message)
    {
        var sb = new StringBuilder();
        sb.Append(message.Mti.PadLeft(4, '0'));

        // Primary bitmap calculation (64 bits)
        ulong bitmap = 0;
        foreach (var fieldNum in message.Fields.Keys)
        {
            if (fieldNum >= 1 && fieldNum <= 64)
            {
                bitmap |= (1UL << (64 - fieldNum));
            }
        }

        sb.Append(bitmap.ToString("X16"));

        // Append fields in order
        foreach (var kvp in message.Fields.OrderBy(x => x.Key))
        {
            sb.Append($"[{kvp.Key}:{kvp.Value}]");
        }

        return sb.ToString();
    }

    public static Iso8583Message Unpack(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload) || rawPayload.Length < 20)
            throw new ArgumentException("Invalid ISO 8583 raw payload format.", nameof(rawPayload));

        var msg = new Iso8583Message
        {
            Mti = rawPayload[..4]
        };

        // Remaining content has [key:value] tokens
        var fieldMatches = System.Text.RegularExpressions.Regex.Matches(rawPayload[20..], @"\[(\d+):([^\]]+)\]");
        foreach (System.Text.RegularExpressions.Match match in fieldMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var fieldNum))
            {
                msg.Fields[fieldNum] = match.Groups[2].Value;
            }
        }

        return msg;
    }
}
