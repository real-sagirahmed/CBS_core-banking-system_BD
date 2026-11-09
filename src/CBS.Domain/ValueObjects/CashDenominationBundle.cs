using System.Collections.Frozen;
using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Bangladeshi currency denomination tracking (BRPD Circular 12/2024 Module 1.7.1-iii, 1.7.2).
// Cashier till balances must be mathematically balanced at denomination level to prevent teller cash manipulation.
public sealed record CashDenominationBundle
{
    public static readonly FrozenSet<int> ValidDenominations = new[]
    {
        1000, 500, 200, 100, 50, 20, 10, 5, 2, 1
    }.ToFrozenSet();

    private readonly FrozenDictionary<int, int> _counts;

    public IReadOnlyDictionary<int, int> Counts => _counts;
    public Money TotalAmount { get; }

    public CashDenominationBundle(IDictionary<int, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts, nameof(counts));

        var builder = new Dictionary<int, int>();
        decimal total = 0m;

        foreach (var (denom, qty) in counts)
        {
            if (!ValidDenominations.Contains(denom))
            {
                throw new InvariantViolationException(
                    "InvalidCurrencyDenominationInvariant",
                    "BRPD Circular 12/2024 (1.7.1-iii)",
                    $"Denomination '{denom}' is not a valid legal tender denomination in Bangladesh.");
            }

            if (qty < 0)
            {
                throw new InvariantViolationException(
                    "NegativeDenominationQuantityInvariant",
                    "BRPD Circular 12/2024 (1.7.1-iii)",
                    $"Quantity for denomination '{denom}' cannot be negative (provided: {qty}).");
            }

            if (qty > 0)
            {
                builder[denom] = qty;
                total += (decimal)denom * qty;
            }
        }

        _counts = builder.ToFrozenDictionary();
        TotalAmount = Money.Bdt(total);
    }

    public static CashDenominationBundle Empty => new(new Dictionary<int, int>());

    public static CashDenominationBundle Create(params (int denomination, int quantity)[] entries)
    {
        var dict = entries.ToDictionary(e => e.denomination, e => e.quantity);
        return new CashDenominationBundle(dict);
    }

    public int GetCount(int denomination) => _counts.GetValueOrDefault(denomination, 0);

    public CashDenominationBundle Add(CashDenominationBundle other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var combined = new Dictionary<int, int>(_counts);
        foreach (var (denom, qty) in other._counts)
        {
            combined[denom] = combined.GetValueOrDefault(denom, 0) + qty;
        }

        return new CashDenominationBundle(combined);
    }

    public CashDenominationBundle Subtract(CashDenominationBundle other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));

        var result = new Dictionary<int, int>(_counts);
        foreach (var (denom, requiredQty) in other._counts)
        {
            int currentQty = result.GetValueOrDefault(denom, 0);
            if (currentQty < requiredQty)
            {
                throw new InvariantViolationException(
                    "InsufficientDenominationQuantityInvariant",
                    "BRPD Circular 12/2024 (1.7.1-iii, 1.7.2)",
                    $"Cannot dispense {requiredQty} note(s)/coin(s) of denomination {denom} BDT. Current drawer only holds {currentQty}.");
            }

            result[denom] = currentQty - requiredQty;
        }

        return new CashDenominationBundle(result);
    }

    public override string ToString()
    {
        if (_counts.Count == 0) return "Empty bundle (0 BDT)";
        var parts = _counts
            .OrderByDescending(kv => kv.Key)
            .Select(kv => $"{kv.Key}x{kv.Value}");
        return $"{string.Join(", ", parts)} (Total: {TotalAmount})";
    }
}
