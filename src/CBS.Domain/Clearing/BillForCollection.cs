// ARCHITECTURAL INTENT: Aggregate Root managing Outward Bills for Collection (OBC) and Inward Bills for Collection (IBC)
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 2 (Sections 2.2.4 & 2.2.5).

using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Clearing;

public sealed class BillForCollection : AggregateRoot<Guid>
{
    public string BillNumber { get; private set; } = null!;
    public BillCollectionType CollectionType { get; private set; }
    public string CustomerAccountNumber { get; private set; } = null!;
    public string CustomerCif { get; private set; } = null!;
    public string DrawerName { get; private set; } = null!;
    public string DraweeName { get; private set; } = null!;
    public string DraweeBankName { get; private set; } = null!;
    public string DraweeBranchName { get; private set; } = null!;
    public string InstrumentNumber { get; private set; } = null!;
    public DateOnly InstrumentDate { get; private set; }
    public Money BillAmount { get; private set; } = null!;
    public BillCollectionStatus Status { get; private set; }
    public string? ScheduleNumber { get; private set; }
    public DateOnly? ScheduleDate { get; private set; }
    public DateOnly LodgedDate { get; private set; }
    public DateOnly? RealizationDate { get; private set; }
    public Money? CollectionCommission { get; private set; }
    public Money? PostageCharges { get; private set; }
    public Money? VatAmount { get; private set; }
    public Money? NetProceeds { get; private set; }
    public string? DishonourReason { get; private set; }
    public string LodgedByUserId { get; private set; } = null!;
    public string? VerifiedByCheckerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    #pragma warning disable CS8618
    private BillForCollection() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public BillForCollection(
        Guid id,
        string billNumber,
        BillCollectionType collectionType,
        string customerAccountNumber,
        string customerCif,
        string drawerName,
        string draweeName,
        string draweeBankName,
        string draweeBranchName,
        string instrumentNumber,
        DateOnly instrumentDate,
        Money billAmount,
        DateOnly lodgedDate,
        string lodgedByUserId)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(billNumber))
            throw new ArgumentException("Bill number is required.", nameof(billNumber));
        if (string.IsNullOrWhiteSpace(customerAccountNumber))
            throw new ArgumentException("Customer account number is required.", nameof(customerAccountNumber));
        if (string.IsNullOrWhiteSpace(customerCif))
            throw new ArgumentException("Customer CIF is required.", nameof(customerCif));
        if (string.IsNullOrWhiteSpace(drawerName))
            throw new ArgumentException("Drawer name is required.", nameof(drawerName));
        if (string.IsNullOrWhiteSpace(draweeName))
            throw new ArgumentException("Drawee name is required.", nameof(draweeName));
        if (string.IsNullOrWhiteSpace(instrumentNumber))
            throw new ArgumentException("Instrument number is required.", nameof(instrumentNumber));
        ArgumentNullException.ThrowIfNull(billAmount, nameof(billAmount));
        if (billAmount.Amount <= 0)
            throw new ArgumentException("Bill amount must be positive.", nameof(billAmount));
        if (string.IsNullOrWhiteSpace(lodgedByUserId))
            throw new ArgumentException("Lodged by user ID is required.", nameof(lodgedByUserId));

        BillNumber = billNumber.Trim();
        CollectionType = collectionType;
        CustomerAccountNumber = customerAccountNumber.Trim();
        CustomerCif = customerCif.Trim();
        DrawerName = drawerName.Trim();
        DraweeName = draweeName.Trim();
        DraweeBankName = draweeBankName.Trim();
        DraweeBranchName = draweeBranchName.Trim();
        InstrumentNumber = instrumentNumber.Trim();
        InstrumentDate = instrumentDate;
        BillAmount = billAmount;
        LodgedDate = lodgedDate;
        LodgedByUserId = lodgedByUserId.Trim();
        Status = BillCollectionStatus.Lodged;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void DispatchSchedule(string scheduleNumber, DateOnly scheduleDate)
    {
        if (Status != BillCollectionStatus.Lodged)
            throw new InvalidOperationException($"Cannot dispatch bill in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(scheduleNumber))
            throw new ArgumentException("Schedule number is required.", nameof(scheduleNumber));

        ScheduleNumber = scheduleNumber.Trim();
        ScheduleDate = scheduleDate;
        Status = BillCollectionStatus.Dispatched;
    }

    public void RealizeBill(
        decimal commissionAmount,
        decimal postageAmount,
        DateOnly realizationDate,
        string checkerUserId)
    {
        if (Status != BillCollectionStatus.Dispatched && Status != BillCollectionStatus.Lodged)
            throw new InvalidOperationException($"Cannot realize bill in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));
        if (commissionAmount < 0 || postageAmount < 0)
            throw new ArgumentException("Charges cannot be negative.");

        var comm = Math.Round(commissionAmount, 2);
        var post = Math.Round(postageAmount, 2);
        var vat = Math.Round((comm + post) * 0.15m, 2);
        var totalDeductions = comm + post + vat;

        if (totalDeductions >= BillAmount.Amount)
        {
            throw new InvariantViolationException(
                "ExcessiveChargesOnCollectionInvariant",
                $"Total collection deductions ({totalDeductions} BDT) cannot exceed or equal bill face value ({BillAmount.Amount} BDT).",
                "BRPD Circular No. 12 Section 2.2.4"
            );
        }

        var netAmount = BillAmount.Amount - totalDeductions;
        CollectionCommission = new Money(comm, BillAmount.Currency);
        PostageCharges = new Money(post, BillAmount.Currency);
        VatAmount = new Money(vat, BillAmount.Currency);
        NetProceeds = new Money(netAmount, BillAmount.Currency);
        RealizationDate = realizationDate;
        VerifiedByCheckerUserId = checkerUserId.Trim();
        Status = BillCollectionStatus.Realized;
    }

    public void MarkDishonoured(string reason, string checkerUserId)
    {
        if (Status != BillCollectionStatus.Dispatched && Status != BillCollectionStatus.Lodged)
            throw new InvalidOperationException($"Cannot mark dishonoured for bill in status '{Status}'.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Dishonour reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker user ID is required.", nameof(checkerUserId));

        DishonourReason = reason.Trim();
        VerifiedByCheckerUserId = checkerUserId.Trim();
        Status = BillCollectionStatus.ReturnedDishonoured;
    }
}
