using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Remittance.ValueObjects;

// ARCHITECTURAL INTENT: Encapsulates standard SWIFT MT103 Single Customer Credit Transfer
// payload for inward foreign remittances and outward TT transmissions (BRPD Circular 12/2024 Module 2.2.1, 2.2.6).
public sealed record SwiftMt103Payload
{
    public string TransactionReferenceNumber { get; init; } // Tag 20
    public string BankOperationCode { get; init; }          // Tag 23B (e.g. CRED)
    public DateOnly ValueDate { get; init; }                // Tag 32A Date
    public Currency Currency { get; init; }                 // Tag 32A Currency
    public decimal InterbankSettledAmount { get; init; }    // Tag 32A Amount
    public string OrderingCustomer { get; init; }           // Tag 50K (Sender Name/Address)
    public string OrderingInstitutionBic { get; init; }     // Tag 52A/51A
    public string SenderBankBic { get; init; }              // Tag 57A
    public string BeneficiaryAccount { get; init; }         // Tag 59 Account/IBAN
    public string BeneficiaryCustomer { get; init; }        // Tag 59 Name
    public string RemittanceInformation { get; init; }      // Tag 70
    public RemittanceChargeParty DetailsOfCharges { get; init; } // Tag 71A

    #pragma warning disable CS8618
    public SwiftMt103Payload() { }
    #pragma warning restore CS8618

    [System.Text.Json.Serialization.JsonConstructor]
    public SwiftMt103Payload(
        string transactionReferenceNumber,
        string bankOperationCode,
        DateOnly valueDate,
        Currency currency,
        decimal interbankSettledAmount,
        string orderingCustomer,
        string orderingInstitutionBic,
        string senderBankBic,
        string beneficiaryAccount,
        string beneficiaryCustomer,
        string remittanceInformation,
        RemittanceChargeParty detailsOfCharges)
    {
        if (string.IsNullOrWhiteSpace(transactionReferenceNumber))
            throw new ArgumentException("SWIFT Tag 20 Transaction Reference Number is required.", nameof(transactionReferenceNumber));
        if (string.IsNullOrWhiteSpace(orderingCustomer))
            throw new ArgumentException("SWIFT Tag 50K Ordering Customer is required.", nameof(orderingCustomer));
        if (string.IsNullOrWhiteSpace(beneficiaryAccount))
            throw new ArgumentException("SWIFT Tag 59 Beneficiary Account is required.", nameof(beneficiaryAccount));
        if (string.IsNullOrWhiteSpace(beneficiaryCustomer))
            throw new ArgumentException("SWIFT Tag 59 Beneficiary Customer is required.", nameof(beneficiaryCustomer));
        if (interbankSettledAmount <= 0)
            throw new ArgumentException("Interbank settled amount must be greater than zero.", nameof(interbankSettledAmount));

        TransactionReferenceNumber = transactionReferenceNumber.Trim().ToUpperInvariant();
        BankOperationCode = string.IsNullOrWhiteSpace(bankOperationCode) ? "CRED" : bankOperationCode.Trim().ToUpperInvariant();
        ValueDate = valueDate;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        InterbankSettledAmount = interbankSettledAmount;
        OrderingCustomer = orderingCustomer.Trim();
        OrderingInstitutionBic = orderingInstitutionBic?.Trim().ToUpperInvariant() ?? "UNKNOWN";
        SenderBankBic = senderBankBic?.Trim().ToUpperInvariant() ?? "UNKNOWN";
        BeneficiaryAccount = beneficiaryAccount.Trim();
        BeneficiaryCustomer = beneficiaryCustomer.Trim();
        RemittanceInformation = remittanceInformation?.Trim() ?? "FOREIGN REMITTANCE";
        DetailsOfCharges = detailsOfCharges;
    }
}
