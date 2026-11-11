using System.Text.Json;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Shariah.ValueObjects;

namespace CBS.Domain.Shariah;

// ARCHITECTURAL INTENT: Under BRPD Circular 12/2024 (4.1.5-iv) and Shariah Rule 3.1:
// Disbursing Murabaha funds directly into a client's Current Account is a severe Shariah violation.
// The bank must purchase the goods, inspect them, and pay the verified vendor/supplier directly via Pay Order or RTGS.
public class MurabahaFinancingFacility : AggregateRoot<Guid>
{
    public string FacilityNumber { get; private set; }
    public string Cif { get; private set; }
    public string BranchCode { get; private set; }
    public decimal PurchaseCost { get; private set; }
    public decimal ProfitMarginAmount { get; private set; }
    public decimal TotalSalePrice => PurchaseCost + ProfitMarginAmount;
    public int RepaymentTenorMonths { get; private set; }
    public decimal MonthlyInstallment { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public ShariahFacilityStatus Status { get; private set; }
    public string GoodsDescription { get; private set; }
    public string? InspectionProofReference { get; private set; }
    public string? VendorDisbursementOrderJson { get; private set; }
    public DateTime? DisbursedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    protected MurabahaFinancingFacility() : base(Guid.NewGuid()) { }
    #pragma warning restore CS8618

    public MurabahaFinancingFacility(
        string facilityNumber,
        string cif,
        string branchCode,
        decimal purchaseCost,
        decimal profitMarginAmount,
        int repaymentTenorMonths,
        string goodsDescription) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(facilityNumber)) throw new ArgumentException("Facility number required.", nameof(facilityNumber));
        if (string.IsNullOrWhiteSpace(cif)) throw new ArgumentException("CIF required.", nameof(cif));
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code required.", nameof(branchCode));
        if (purchaseCost <= 0) throw new ArgumentOutOfRangeException(nameof(purchaseCost), "Purchase cost must be positive.");
        if (profitMarginAmount < 0) throw new ArgumentOutOfRangeException(nameof(profitMarginAmount), "Profit margin cannot be negative.");
        if (repaymentTenorMonths <= 0) throw new ArgumentOutOfRangeException(nameof(repaymentTenorMonths), "Tenor must be at least 1 month.");
        if (string.IsNullOrWhiteSpace(goodsDescription)) throw new ArgumentException("Goods description required.", nameof(goodsDescription));

        FacilityNumber = facilityNumber.Trim();
        Cif = cif.Trim();
        BranchCode = branchCode.Trim();
        PurchaseCost = purchaseCost;
        ProfitMarginAmount = profitMarginAmount;
        RepaymentTenorMonths = repaymentTenorMonths;
        MonthlyInstallment = Math.Round((purchaseCost + profitMarginAmount) / repaymentTenorMonths, 2);
        OutstandingBalance = 0m;
        Status = ShariahFacilityStatus.Draft;
        GoodsDescription = goodsDescription.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordGoodsInspection(string inspectionProofReference, string inspectorOfficerId)
    {
        if (string.IsNullOrWhiteSpace(inspectionProofReference))
            throw new ArgumentException("Inspection proof reference required.", nameof(inspectionProofReference));
        if (string.IsNullOrWhiteSpace(inspectorOfficerId))
            throw new ArgumentException("Inspector officer ID required.", nameof(inspectorOfficerId));

        if (Status != ShariahFacilityStatus.Draft)
        {
            throw new InvariantViolationException(
                "InvalidFacilityStatusInvariant",
                "BRPD 4.1.5-iv",
                $"Cannot record inspection for facility '{FacilityNumber}' in status '{Status}'.");
        }

        InspectionProofReference = inspectionProofReference.Trim();
        Status = ShariahFacilityStatus.GoodsInspected;
    }

    public void DisburseToVendor(VendorDisbursementOrder order, string authorizerOfficerId)
    {
        ArgumentNullException.ThrowIfNull(order, nameof(order));
        if (string.IsNullOrWhiteSpace(authorizerOfficerId))
            throw new ArgumentException("Authorizer officer ID required.", nameof(authorizerOfficerId));

        if (Status != ShariahFacilityStatus.GoodsInspected)
        {
            throw new InvariantViolationException(
                "UninspectedGoodsDisbursementInvariant",
                "BRPD 4.1.5-iv",
                $"Cannot disburse Murabaha facility '{FacilityNumber}'. Goods must be physically inspected prior to vendor payment.");
        }

        if (order.InvoiceAmount != PurchaseCost)
        {
            throw new InvariantViolationException(
                "VendorInvoiceMismatchInvariant",
                "BRPD 4.1.5-iv",
                $"Vendor invoice amount {order.InvoiceAmount:F2} does not match contract purchase cost {PurchaseCost:F2}.");
        }

        VendorDisbursementOrderJson = JsonSerializer.Serialize(order);
        DisbursedAtUtc = DateTime.UtcNow;
        OutstandingBalance = TotalSalePrice;
        Status = ShariahFacilityStatus.DisbursedToVendor;
    }

    public void RecordRepayment(decimal amount)
    {
        if (Status != ShariahFacilityStatus.DisbursedToVendor && Status != ShariahFacilityStatus.Overdue)
        {
            throw new InvariantViolationException(
                "InvalidFacilityStatusInvariant",
                "BRPD 4.1.5",
                $"Cannot record repayment on facility in status '{Status}'.");
        }

        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Repayment amount must be positive.");

        OutstandingBalance = Math.Max(0m, OutstandingBalance - amount);
        if (OutstandingBalance == 0m)
        {
            Status = ShariahFacilityStatus.Settled;
        }
    }

    public void MarkOverdue()
    {
        if (Status == ShariahFacilityStatus.DisbursedToVendor)
        {
            Status = ShariahFacilityStatus.Overdue;
        }
    }
}
