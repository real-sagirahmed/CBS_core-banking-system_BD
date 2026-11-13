// ARCHITECTURAL INTENT: Statutory TT Commission, Telecom Message Charge, and 15% VAT Calculator
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3 (3.13.1-i).

using System;
using CBS.Domain.Enums;

namespace CBS.Domain.TelegraphicTransfers;

public static class TtFeeCalculator
{
    public const decimal VatRatePercent = 15.00m;

    public static (decimal Commission, decimal MessageCharge, decimal Vat, TtCommissionSlab Slab) Calculate(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Transfer amount must be positive.");

        decimal commission;
        decimal messageCharge;
        TtCommissionSlab slab;

        if (amount <= 25_000m)
        {
            commission = 30.00m;
            messageCharge = 20.00m;
            slab = TtCommissionSlab.UpTo25K;
        }
        else if (amount <= 100_000m)
        {
            commission = 60.00m;
            messageCharge = 30.00m;
            slab = TtCommissionSlab.From25KTo100K;
        }
        else if (amount <= 500_000m)
        {
            commission = 120.00m;
            messageCharge = 50.00m;
            slab = TtCommissionSlab.From100KTo500K;
        }
        else if (amount <= 1_000_000m)
        {
            commission = 250.00m;
            messageCharge = 50.00m;
            slab = TtCommissionSlab.From500KTo1M;
        }
        else
        {
            commission = 350.00m;
            messageCharge = 50.00m;
            slab = TtCommissionSlab.Above1M;
        }

        var baseFee = commission + messageCharge;
        var vat = decimal.Round(baseFee * (VatRatePercent / 100m), 2, MidpointRounding.AwayFromZero);

        return (commission, messageCharge, vat, slab);
    }
}
