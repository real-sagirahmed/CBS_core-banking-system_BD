using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Shariah;

// ARCHITECTURAL INTENT: In accordance with BRPD Circular 12/2024 (4.3.4-i, 4.3.10) and Shariah Rule 3.2:
// Quard contracts require a strict 0.00% interest or mark-up invariant.
// The bank is prohibited from earning any financial return; only actual documented administrative costs may be collected.
public class QuardFinancingFacility : AggregateRoot<Guid>
{
    public string FacilityNumber { get; private set; }
    public string Cif { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal ActualAdministrativeFee { get; private set; }
    public decimal InterestRate { get; private set; } = 0.00m; // Strict 0%
    public decimal ProfitRate { get; private set; } = 0.00m;   // Strict 0%
    public decimal OutstandingBalance { get; private set; }
    public int TenorMonths { get; private set; }
    public bool IsSettled { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected QuardFinancingFacility() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public QuardFinancingFacility(
        string facilityNumber,
        string cif,
        decimal principalAmount,
        decimal actualAdministrativeFee,
        int tenorMonths) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(facilityNumber)) throw new ArgumentException("Facility number required.", nameof(facilityNumber));
        if (string.IsNullOrWhiteSpace(cif)) throw new ArgumentException("CIF required.", nameof(cif));
        if (principalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(principalAmount), "Principal amount must be positive.");
        if (actualAdministrativeFee < 0) throw new ArgumentOutOfRangeException(nameof(actualAdministrativeFee), "Admin fee cannot be negative.");
        if (tenorMonths <= 0) throw new ArgumentOutOfRangeException(nameof(tenorMonths), "Tenor must be at least 1 month.");

        FacilityNumber = facilityNumber.Trim();
        Cif = cif.Trim();
        PrincipalAmount = principalAmount;
        ActualAdministrativeFee = actualAdministrativeFee;
        InterestRate = 0.00m;
        ProfitRate = 0.00m;
        OutstandingBalance = principalAmount;
        TenorMonths = tenorMonths;
        IsSettled = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordRepayment(decimal amount)
    {
        if (IsSettled)
        {
            throw new InvariantViolationException(
                "SettledFacilityRepaymentInvariant",
                "BRPD 4.3.4",
                $"Quard facility '{FacilityNumber}' is already settled.");
        }

        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Repayment amount must be positive.");

        OutstandingBalance = Math.Max(0m, OutstandingBalance - amount);
        if (OutstandingBalance == 0m)
        {
            IsSettled = true;
        }
    }
}
