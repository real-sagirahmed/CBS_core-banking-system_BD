using System.Text.Json;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Shariah.ValueObjects;

namespace CBS.Domain.Shariah;

// ARCHITECTURAL INTENT: In accordance with Bangladesh Bank BRPD Circular 12/2024 (1.2.2-vii, 3.2.2-ii) and Shariah Rule 2.2:
// Fixed, predetermined return rates at account inception are strictly prohibited.
// Return to Mudaraba depositors is strictly derived from actual distributable investment pool earnings,
// split according to the agreed Mudaraba ratio (Mudarib vs Rab-ul-Mal), factored by product weightages,
// and protected through Profit Equalization Reserve (PER) and Investment Risk Reserve (IRR).
public class MudarabaInvestmentPool : AggregateRoot<Guid>
{
    public string PoolName { get; private set; }
    public MudarabaPoolType PoolType { get; private set; }
    public string Currency { get; private set; }
    public decimal TotalDepositBalance { get; private set; }
    public decimal MudaribShareRatio { get; private set; } // e.g. 35.0%
    public decimal RabUlMalShareRatio => 100.0m - MudaribShareRatio;
    public decimal PerRatio { get; private set; } // Profit Equalization Reserve, e.g. 2.0%
    public decimal IrrRatio { get; private set; } // Investment Risk Reserve, e.g. 1.0%
    public string WeightagesJson { get; private set; }
    public decimal AccumulatedPerBalance { get; private set; }
    public decimal AccumulatedIrrBalance { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastDistributionDateUtc { get; private set; }

    #pragma warning disable CS8618
    protected MudarabaInvestmentPool() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public MudarabaInvestmentPool(
        string poolName,
        MudarabaPoolType poolType,
        string currency,
        decimal mudaribShareRatio,
        decimal perRatio = 2.0m,
        decimal irrRatio = 1.0m) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(poolName)) throw new ArgumentException("Pool name required.", nameof(poolName));
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency required.", nameof(currency));
        if (mudaribShareRatio <= 0 || mudaribShareRatio >= 100)
        {
            throw new InvariantViolationException(
                "InvalidMudaribShareRatioInvariant",
                "BRPD Circular 12/2024 (Shariah Directive 2.2)",
                "Mudarib profit share ratio must be strictly between 0% and 100%.");
        }

        PoolName = poolName.Trim();
        PoolType = poolType;
        Currency = currency.Trim().ToUpperInvariant();
        TotalDepositBalance = 0m;
        MudaribShareRatio = mudaribShareRatio;
        PerRatio = perRatio;
        IrrRatio = irrRatio;
        WeightagesJson = "[]";
        AccumulatedPerBalance = 0m;
        AccumulatedIrrBalance = 0m;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AddDeposit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Deposit amount must be positive.");
        TotalDepositBalance += amount;
    }

    public void DeductWithdrawal(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Withdrawal amount must be positive.");
        if (amount > TotalDepositBalance)
        {
            throw new InvariantViolationException(
                "InsufficientPoolBalanceInvariant",
                "BRPD Circular 12/2024 (Shariah Directive 1.1)",
                $"Cannot withdraw {amount:F2} from pool '{PoolName}' with balance {TotalDepositBalance:F2}.");
        }
        TotalDepositBalance -= amount;
    }

    public void SetWeightage(MudarabaWeightage weightage)
    {
        ArgumentNullException.ThrowIfNull(weightage, nameof(weightage));

        var list = string.IsNullOrWhiteSpace(WeightagesJson) || WeightagesJson == "[]"
            ? new List<MudarabaWeightage>()
            : JsonSerializer.Deserialize<List<MudarabaWeightage>>(WeightagesJson) ?? [];

        list.RemoveAll(w => w.ProductCode.Equals(weightage.ProductCode, StringComparison.OrdinalIgnoreCase));
        list.Add(weightage);

        WeightagesJson = JsonSerializer.Serialize(list);
    }

    public PoolProfitDistributionResult DistributeProfit(decimal grossInvestmentIncome, DateTime asOfUtc)
    {
        if (grossInvestmentIncome <= 0)
        {
            throw new InvariantViolationException(
                "InvalidGrossIncomeInvariant",
                "BRPD Circular 12/2024 (Shariah Directive 2.2)",
                "Gross investment pool income must be strictly greater than zero to execute profit distribution.");
        }

        decimal mudaribShareAmount = Math.Round(grossInvestmentIncome * (MudaribShareRatio / 100m), 2);
        decimal rabUlMalGross = grossInvestmentIncome - mudaribShareAmount;

        decimal perDeduction = Math.Round(rabUlMalGross * (PerRatio / 100m), 2);
        decimal irrDeduction = Math.Round(rabUlMalGross * (IrrRatio / 100m), 2);

        decimal netDistributable = rabUlMalGross - perDeduction - irrDeduction;

        AccumulatedPerBalance += perDeduction;
        AccumulatedIrrBalance += irrDeduction;
        LastDistributionDateUtc = asOfUtc;

        return new PoolProfitDistributionResult(
            Id,
            grossInvestmentIncome,
            MudaribShareRatio,
            mudaribShareAmount,
            rabUlMalGross,
            perDeduction,
            irrDeduction,
            netDistributable,
            asOfUtc);
    }
}
