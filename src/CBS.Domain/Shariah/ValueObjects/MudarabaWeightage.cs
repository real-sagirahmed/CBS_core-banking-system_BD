namespace CBS.Domain.Shariah.ValueObjects;

public sealed record MudarabaWeightage
{
    public string ProductCode { get; init; }
    public string ProductName { get; init; }
    public decimal Weightage { get; init; }
    public decimal DeclaredProvisionalRate { get; init; }

    #pragma warning disable CS8618
    public MudarabaWeightage() { }
    #pragma warning restore CS8618

    public MudarabaWeightage(string productCode, string productName, decimal weightage, decimal declaredProvisionalRate)
    {
        if (string.IsNullOrWhiteSpace(productCode)) throw new ArgumentException("Product code required.", nameof(productCode));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name required.", nameof(productName));
        if (weightage <= 0) throw new ArgumentOutOfRangeException(nameof(weightage), "Weightage must be greater than zero.");
        if (declaredProvisionalRate < 0) throw new ArgumentOutOfRangeException(nameof(declaredProvisionalRate), "Rate cannot be negative.");

        ProductCode = productCode.Trim();
        ProductName = productName.Trim();
        Weightage = weightage;
        DeclaredProvisionalRate = declaredProvisionalRate;
    }
}
