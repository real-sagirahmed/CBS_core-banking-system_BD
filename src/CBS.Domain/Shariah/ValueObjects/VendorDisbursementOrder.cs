namespace CBS.Domain.Shariah.ValueObjects;

public sealed record VendorDisbursementOrder
{
    public string VendorName { get; init; }
    public string VendorAccountNumber { get; init; }
    public string VendorBankRoutingNo { get; init; }
    public string InvoiceNumber { get; init; }
    public decimal InvoiceAmount { get; init; }
    public string InspectionProofReference { get; init; }
    public DateTime OrderDateUtc { get; init; }

    #pragma warning disable CS8618
    public VendorDisbursementOrder() { }
    #pragma warning restore CS8618

    public VendorDisbursementOrder(
        string vendorName,
        string vendorAccountNumber,
        string vendorBankRoutingNo,
        string invoiceNumber,
        decimal invoiceAmount,
        string inspectionProofReference,
        DateTime orderDateUtc)
    {
        if (string.IsNullOrWhiteSpace(vendorName)) throw new ArgumentException("Vendor name required.", nameof(vendorName));
        if (string.IsNullOrWhiteSpace(vendorAccountNumber)) throw new ArgumentException("Vendor account number required.", nameof(vendorAccountNumber));
        if (string.IsNullOrWhiteSpace(vendorBankRoutingNo)) throw new ArgumentException("Vendor routing number required.", nameof(vendorBankRoutingNo));
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new ArgumentException("Invoice number required.", nameof(invoiceNumber));
        if (invoiceAmount <= 0) throw new ArgumentOutOfRangeException(nameof(invoiceAmount), "Invoice amount must be positive.");
        if (string.IsNullOrWhiteSpace(inspectionProofReference)) throw new ArgumentException("Inspection proof reference required.", nameof(inspectionProofReference));

        VendorName = vendorName.Trim();
        VendorAccountNumber = vendorAccountNumber.Trim();
        VendorBankRoutingNo = vendorBankRoutingNo.Trim();
        InvoiceNumber = invoiceNumber.Trim();
        InvoiceAmount = invoiceAmount;
        InspectionProofReference = inspectionProofReference.Trim();
        OrderDateUtc = orderDateUtc;
    }
}
