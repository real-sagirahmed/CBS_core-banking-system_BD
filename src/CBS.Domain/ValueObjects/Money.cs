using CBS.Domain.Exceptions;

namespace CBS.Domain.ValueObjects;

public sealed record Currency
{
    public string Code { get; }
    public string Name { get; }
    public string Symbol { get; }
    public int DecimalPlaces { get; }
    public bool IsBaseCurrency { get; }

    public static readonly Currency BDT = new("BDT", "Bangladeshi Taka", "৳", 2, isBaseCurrency: true);
    public static readonly Currency USD = new("USD", "US Dollar", "$", 2, isBaseCurrency: false);
    public static readonly Currency EUR = new("EUR", "Euro", "€", 2, isBaseCurrency: false);
    public static readonly Currency GBP = new("GBP", "British Pound", "£", 2, isBaseCurrency: false);

    private Currency(string code, string name, string symbol, int decimalPlaces, bool isBaseCurrency)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 3)
            throw new ArgumentException("Currency code must be exactly 3 characters ISO 4217 code.", nameof(code));

        Code = code.Trim().ToUpperInvariant();
        Name = name;
        Symbol = symbol;
        DecimalPlaces = decimalPlaces;
        IsBaseCurrency = isBaseCurrency;
    }

    public static Currency Create(string code, string name, string symbol, int decimalPlaces = 2, bool isBaseCurrency = false)
    {
        return new Currency(code, name, symbol, decimalPlaces, isBaseCurrency);
    }

    public override string ToString() => Code;
}

public sealed record Money
{
    // ARCHITECTURAL INTENT: Financial amounts MUST use fixed-point decimal.
    // Floating point types (float/double) are prohibited in core banking to eliminate binary representation errors.
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency, nameof(currency));
        Amount = decimal.Round(amount, currency.DecimalPlaces, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);
    public static Money Bdt(decimal amount) => new(amount, Currency.BDT);
    public static Money Usd(decimal amount) => new(amount, Currency.USD);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m) throw new DivideByZeroException("Cannot divide monetary amount by zero.");
        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvariantViolationException(
                "CurrencyMismatch",
                "BRPD 1.3.2",
                $"Cannot operate between different currencies '{left.Currency.Code}' and '{right.Currency.Code}'. Explicit conversion required.");
        }
    }

    public override string ToString() => $"{Currency.Symbol} {Amount:N2}";
}
