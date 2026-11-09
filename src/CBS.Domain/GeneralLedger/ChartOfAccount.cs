using CBS.Domain.Common;
using CBS.Domain.Enums;

namespace CBS.Domain.GeneralLedger;

public class ChartOfAccount : Entity<string>
{
    public string Name { get; private set; }
    public AccountCategory Category { get; private set; }
    public string? ParentCode { get; private set; }
    public int Level { get; private set; }
    public bool IsLeaf { get; private set; }
    public bool IsActive { get; private set; }
    public string CurrencyCode { get; private set; }

    public ChartOfAccount(
        string code,
        string name,
        AccountCategory category,
        string? parentCode = null,
        int level = 1,
        bool isLeaf = true,
        string currencyCode = "BDT") : base(code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("GL Code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("GL Name is required.", nameof(name));

        Name = name.Trim();
        Category = category;
        ParentCode = parentCode;
        Level = level;
        IsLeaf = isLeaf;
        IsActive = true;
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
