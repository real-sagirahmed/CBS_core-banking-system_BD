using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Deposits;

public class DpsInstallmentSchedule : Entity<string>
{
    public string DpsAccountId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateOnly? PaidDate { get; private set; }
    public Money DueAmount { get; private set; }
    public Money LateFee { get; private set; }
    public DpsInstallmentStatus Status { get; private set; }
    public string? PaymentVoucherNumber { get; private set; }

#pragma warning disable CS8618
    private DpsInstallmentSchedule() : base(string.Empty) { }
#pragma warning restore CS8618

    public DpsInstallmentSchedule(
        string id,
        string dpsAccountId,
        int installmentNumber,
        DateOnly dueDate,
        Money dueAmount) : base(id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Schedule ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(dpsAccountId)) throw new ArgumentException("DPS Account ID is required.", nameof(dpsAccountId));
        if (installmentNumber <= 0) throw new ArgumentOutOfRangeException(nameof(installmentNumber), "Installment number must be positive.");
        ArgumentNullException.ThrowIfNull(dueAmount);

        DpsAccountId = dpsAccountId;
        InstallmentNumber = installmentNumber;
        DueDate = dueDate;
        DueAmount = new Money(dueAmount.Amount, dueAmount.Currency);
        LateFee = new Money(0, dueAmount.Currency);
        Status = DpsInstallmentStatus.Pending;
    }

    public void MarkPaid(DateOnly paymentDate, Money lateFee, string? voucherNumber = null)
    {
        PaidDate = paymentDate;
        LateFee = lateFee ?? new Money(0, DueAmount.Currency);
        PaymentVoucherNumber = voucherNumber;
        Status = DpsInstallmentStatus.Paid;
    }

    public void MarkOverdue(Money lateFee)
    {
        LateFee = lateFee ?? LateFee;
        Status = DpsInstallmentStatus.Overdue;
    }
}
