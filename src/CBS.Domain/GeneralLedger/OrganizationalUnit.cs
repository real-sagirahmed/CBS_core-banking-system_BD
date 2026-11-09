using CBS.Domain.Common;

namespace CBS.Domain.GeneralLedger;

public enum OrgUnitLevel
{
    HeadOffice = 1,
    Division = 2,
    Region = 3,
    Cluster = 4,
    Branch = 5,
    SubBranch = 6,
    Booth = 7
}

// ARCHITECTURAL INTENT: Organizational hierarchy strictly aligned with Bangladesh Bank reporting tree (BRPD 1.3.8).
public class OrganizationalUnit : Entity<string>
{
    public string Name { get; private set; }
    public OrgUnitLevel Level { get; private set; }
    public string? ParentUnitCode { get; private set; }
    public string RoutingNumber { get; private set; }
    public bool IsActive { get; private set; }

    public OrganizationalUnit(
        string code,
        string name,
        OrgUnitLevel level,
        string routingNumber,
        string? parentUnitCode = null) : base(code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Unit code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Unit name is required.", nameof(name));

        Name = name.Trim();
        Level = level;
        RoutingNumber = routingNumber?.Trim() ?? string.Empty;
        ParentUnitCode = parentUnitCode?.Trim();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
