// ARCHITECTURAL INTENT: Statutory Slab Commission and 15% VAT Calculation Engine
// for Payment Orders and Demand Drafts under Bangladesh Bank BRPD Circular No. 12 (1.5.6-iv, 3.11.1-i, 3.12.1-i).

using System;
using CBS.Domain.Enums;

namespace CBS.Domain.PaymentInstruments;

public static class InstrumentCommissionCalculator
{
    public const decimal VatRatePercent = 15.00m;

    public static (decimal Commission, decimal Vat, InstrumentSlabType Slab) Calculate(PaymentInstrumentType type, decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Instrument amount must be positive.");

        decimal commission;
        InstrumentSlabType slab;

        if (amount <= 25_000m)
        {
            commission = type == PaymentInstrumentType.PayOrder ? 25.00m : 30.00m;
            slab = InstrumentSlabType.UpTo25K;
        }
        else if (amount <= 100_000m)
        {
            commission = type == PaymentInstrumentType.PayOrder ? 50.00m : 60.00m;
            slab = InstrumentSlabType.From25KTo100K;
        }
        else if (amount <= 500_000m)
        {
            commission = type == PaymentInstrumentType.PayOrder ? 100.00m : 120.00m;
            slab = InstrumentSlabType.From100KTo500K;
        }
        else if (amount <= 1_000_000m)
        {
            commission = type == PaymentInstrumentType.PayOrder ? 200.00m : 250.00m;
            slab = InstrumentSlabType.From500KTo1M;
        }
        else
        {
            commission = type == PaymentInstrumentType.PayOrder ? 300.00m : 350.00m;
            slab = InstrumentSlabType.Above1M;
        }

        decimal vat = decimal.Round(commission * (VatRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        return (commission, vat, slab);
    }
}
