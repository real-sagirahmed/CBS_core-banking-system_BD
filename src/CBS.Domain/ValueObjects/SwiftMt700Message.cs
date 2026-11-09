namespace CBS.Domain.ValueObjects;

/// <summary>
/// SWIFT MT700 (Issue of a Documentary Credit) representation adhering to SWIFT Standards MT Category 7.
/// Reference: BRPD Circular 12, 5.1.1-iv, 5.1.8-i.
/// </summary>
public sealed record SwiftMt700Message
{
    public string SenderBic { get; }
    public string ReceiverBic { get; }
    public string LcNumber { get; }
    public string RawMessageText { get; }
    public DateTime GeneratedAtUtc { get; }

    private SwiftMt700Message(string senderBic, string receiverBic, string lcNumber, string rawMessageText, DateTime generatedAtUtc)
    {
        SenderBic = senderBic;
        ReceiverBic = receiverBic;
        LcNumber = lcNumber;
        RawMessageText = rawMessageText;
        GeneratedAtUtc = generatedAtUtc;
    }

    public static SwiftMt700Message Generate(
        string senderBic,
        string receiverBic,
        string lcNumber,
        DateTime issueDate,
        DateTime expiryDate,
        string expiryPlace,
        string applicantInfo,
        string beneficiaryInfo,
        string currency,
        decimal amount,
        string availableWith,
        bool partialShipmentAllowed,
        bool transshipmentAllowed,
        string portOfLoading,
        string portOfDischarge,
        DateTime latestShipmentDate,
        string goodsDescription,
        string documentsRequired,
        string additionalConditions,
        string confirmationInstruction = "WITHOUT")
    {
        if (string.IsNullOrWhiteSpace(senderBic) || senderBic.Trim().Length < 8)
            throw new ArgumentException("Sender BIC must be a valid 8 or 11 character SWIFT code.", nameof(senderBic));
        if (string.IsNullOrWhiteSpace(receiverBic) || receiverBic.Trim().Length < 8)
            throw new ArgumentException("Receiver BIC must be a valid 8 or 11 character SWIFT code.", nameof(receiverBic));
        if (string.IsNullOrWhiteSpace(lcNumber))
            throw new ArgumentException("LC number is required.", nameof(lcNumber));

        var msg =
            $"{{1:F01{senderBic.Trim().ToUpperInvariant()}0000000000}}{{2:I700{receiverBic.Trim().ToUpperInvariant()}N}}{{4:\n" +
            $":27:1/1\n" +
            $":40A:IRREVOCABLE\n" +
            $":20:{lcNumber.Trim()}\n" +
            $":31C:{issueDate:yyMMdd}\n" +
            $":31D:{expiryDate:yyMMdd}{expiryPlace.Trim().ToUpperInvariant()}\n" +
            $":50:{applicantInfo.Trim()}\n" +
            $":59:{beneficiaryInfo.Trim()}\n" +
            $":32B:{currency.Trim().ToUpperInvariant()}{amount:F2}\n" +
            $":41A:{availableWith.Trim().ToUpperInvariant()}\n" +
            $":43P:{(partialShipmentAllowed ? "ALLOWED" : "NOT ALLOWED")}\n" +
            $":43T:{(transshipmentAllowed ? "ALLOWED" : "NOT ALLOWED")}\n" +
            $":44E:{portOfLoading.Trim().ToUpperInvariant()}\n" +
            $":44F:{portOfDischarge.Trim().ToUpperInvariant()}\n" +
            $":44C:{latestShipmentDate:yyMMdd}\n" +
            $":45A:{goodsDescription.Trim()}\n" +
            $":46A:{documentsRequired.Trim()}\n" +
            $":47A:{additionalConditions.Trim()}\n" +
            $":71D:ALL BANK CHARGES OUTSIDE BANGLADESH ARE FOR BENEFICIARY ACCOUNT\n" +
            $":49:{confirmationInstruction.Trim().ToUpperInvariant()}\n" +
            $"-}}";

        return new SwiftMt700Message(senderBic.Trim(), receiverBic.Trim(), lcNumber.Trim(), msg, DateTime.UtcNow);
    }

    public override string ToString() => RawMessageText;
}
