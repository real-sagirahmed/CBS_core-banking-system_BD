using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

// ARCHITECTURAL INTENT: Master Export Letter of Credit (LC) and Sales Contract Aggregate Root
// strictly enforcing Statutory Value Addition Ratio (maximum 75% B2B ceiling / minimum 25% value addition),
// B2B expiry/shipment date hierarchy, dual-control maker-checker invariants, and closure protections
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 5.2.2-i, 5.5.1, and 5.6.1.
public sealed class MasterExportLc : AggregateRoot<string>
{
    public string MasterLcNumber => Id;
    public MasterContractType ContractType { get; private set; }
    public CifNumber ExporterCif { get; private set; } = null!;
    public string IssuingBankBic { get; private set; } = null!;
    public string IssuingBankName { get; private set; } = null!;
    public string BuyerName { get; private set; } = null!;
    public string BuyerCountry { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public decimal ValueAdditionPercentage { get; private set; }
    public Money CumulativeB2bAllocatedAmount { get; private set; } = null!;
    public DateOnly IssueDate { get; private set; }
    public DateOnly ExpiryDate { get; private set; }
    public DateOnly LatestShipmentDate { get; private set; }
    public MasterLcStatus Status { get; private set; }
    public string BranchCode { get; private set; } = null!;
    public string MakerUserId { get; private set; } = null!;
    public string? CheckerUserId { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }
    public string? ReplacedContractRef { get; private set; }

    public Money MaxPermissibleB2bAmount =>
        new Money(
            Math.Round(TotalAmount.Amount * (1m - (ValueAdditionPercentage / 100m)), 2, MidpointRounding.AwayFromZero),
            TotalAmount.Currency);

    public Money RemainingB2bCapacity
    {
        get
        {
            decimal diff = MaxPermissibleB2bAmount.Amount - CumulativeB2bAllocatedAmount.Amount;
            return new Money(Math.Max(0m, diff), TotalAmount.Currency);
        }
    }

    #pragma warning disable CS8618
    private MasterExportLc() : base(string.Empty) { }
    #pragma warning restore CS8618

    public MasterExportLc(
        string masterLcNumber,
        MasterContractType contractType,
        CifNumber exporterCif,
        string issuingBankBic,
        string issuingBankName,
        string buyerName,
        string buyerCountry,
        Money totalAmount,
        decimal valueAdditionPercentage,
        DateOnly issueDate,
        DateOnly expiryDate,
        DateOnly latestShipmentDate,
        string branchCode,
        string makerUserId,
        string? replacedContractRef = null)
        : base(masterLcNumber.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(masterLcNumber))
            throw new ArgumentException("Master LC/Contract number is required.", nameof(masterLcNumber));

        ArgumentNullException.ThrowIfNull(exporterCif, nameof(exporterCif));
        ArgumentNullException.ThrowIfNull(totalAmount, nameof(totalAmount));

        if (totalAmount.Amount <= 0m)
            throw new ArgumentException("Master LC amount must be greater than zero.", nameof(totalAmount));

        if (valueAdditionPercentage is < 10.0m or > 90.0m)
            throw new ArgumentException("Value addition percentage must be between 10% and 90%.", nameof(valueAdditionPercentage));

        if (expiryDate <= issueDate)
            throw new ArgumentException("Expiry date must be after issue date.", nameof(expiryDate));

        if (latestShipmentDate > expiryDate)
            throw new ArgumentException("Latest shipment date cannot exceed LC expiry date.", nameof(latestShipmentDate));

        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));

        ContractType = contractType;
        ExporterCif = exporterCif;
        IssuingBankBic = issuingBankBic.Trim().ToUpperInvariant();
        IssuingBankName = issuingBankName.Trim();
        BuyerName = buyerName.Trim();
        BuyerCountry = buyerCountry.Trim().ToUpperInvariant();
        TotalAmount = totalAmount;
        ValueAdditionPercentage = valueAdditionPercentage;
        CumulativeB2bAllocatedAmount = Money.Zero(totalAmount.Currency);
        IssueDate = issueDate;
        ExpiryDate = expiryDate;
        LatestShipmentDate = latestShipmentDate;
        BranchCode = string.IsNullOrWhiteSpace(branchCode) ? "0101" : branchCode.Trim();
        MakerUserId = makerUserId.Trim();
        Status = MasterLcStatus.Draft;
        ReplacedContractRef = replacedContractRef?.Trim().ToUpperInvariant();
    }

    public void Authorize(string checkerUserId)
    {
        if (Status != MasterLcStatus.Draft)
            throw new InvalidOperationException($"Cannot authorize Master LC in status '{Status}'. Only Draft LCs can be authorized.");

        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (string.Equals(MakerUserId, checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Maker cannot act as Checker to authorize Master Export LC / Sales Contract.");
        }

        CheckerUserId = checkerUserId.Trim();
        AuthorizedAtUtc = DateTime.UtcNow;
        Status = MasterLcStatus.Active;
    }

    public void AllocateB2bLimit(Money b2bAmount, DateOnly b2bShipmentDate, DateOnly b2bExpiryDate, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(b2bAmount, nameof(b2bAmount));

        if (Status != MasterLcStatus.Active)
        {
            throw new InvariantViolationException(
                "MasterLcNotActiveInvariant",
                "BRPD Circular 12/2024 (5.6.1-vii)",
                $"Cannot issue Back-to-Back LC against Master LC '{Id}' in status '{Status}'. Must be Active.");
        }

        if (asOfDate > ExpiryDate)
        {
            throw new InvariantViolationException(
                "MasterLcExpiredInvariant",
                "BRPD Circular 12/2024 (5.6.1-vii)",
                $"Master Export LC '{Id}' has expired on {ExpiryDate:yyyy-MM-dd}. Back-to-Back LC issuance strictly prohibited.");
        }

        if (b2bShipmentDate >= LatestShipmentDate)
        {
            throw new InvariantViolationException(
                "B2bShipmentDateInvariant",
                "BRPD Circular 12/2024 (5.6.1-vi)",
                $"Back-to-Back LC shipment date ({b2bShipmentDate:yyyy-MM-dd}) must be strictly earlier than Master LC shipment date ({LatestShipmentDate:yyyy-MM-dd}).");
        }

        if (b2bExpiryDate >= ExpiryDate)
        {
            throw new InvariantViolationException(
                "B2bExpiryDateInvariant",
                "BRPD Circular 12/2024 (5.6.1-v)",
                $"Back-to-Back LC expiry date ({b2bExpiryDate:yyyy-MM-dd}) must be strictly earlier than Master LC expiry date ({ExpiryDate:yyyy-MM-dd}).");
        }

        var newCumulativeB2b = CumulativeB2bAllocatedAmount + b2bAmount;
        if (newCumulativeB2b > MaxPermissibleB2bAmount)
        {
            throw new InvariantViolationException(
                "B2bValueAdditionCeilingInvariant",
                "BRPD Circular 12/2024 (5.6.1-ii, 5.2.2-i)",
                $"Cumulative Back-to-Back LC allocation ({newCumulativeB2b.Amount} {newCumulativeB2b.Currency}) exceeds statutory ceiling of {MaxPermissibleB2bAmount.Amount} {MaxPermissibleB2bAmount.Currency} (Minimum {ValueAdditionPercentage}% local value addition required).");
        }

        CumulativeB2bAllocatedAmount = newCumulativeB2b;
    }

    public void ReleaseB2bLimit(Money b2bAmount)
    {
        ArgumentNullException.ThrowIfNull(b2bAmount, nameof(b2bAmount));
        decimal newAmount = Math.Max(0m, CumulativeB2bAllocatedAmount.Amount - b2bAmount.Amount);
        CumulativeB2bAllocatedAmount = new Money(newAmount, CumulativeB2bAllocatedAmount.Currency);
    }

    public void AmendAmount(Money newAmount, string makerUserId, string checkerUserId)
    {
        ArgumentNullException.ThrowIfNull(newAmount, nameof(newAmount));

        if (Status != MasterLcStatus.Active)
            throw new InvalidOperationException($"Cannot amend Master LC in status '{Status}'.");

        if (string.Equals(makerUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Maker cannot act as Checker to authorize Master Export LC amendment.");
        }

        if (newAmount.Amount < TotalAmount.Amount)
        {
            decimal newCeiling = Math.Round(newAmount.Amount * (1m - (ValueAdditionPercentage / 100m)), 2, MidpointRounding.AwayFromZero);
            if (CumulativeB2bAllocatedAmount.Amount > newCeiling)
            {
                throw new InvariantViolationException(
                    "MasterLcValueDecreaseAmendmentInvariant",
                    "BRPD Circular 12/2024 (5.6.1-iii)",
                    $"Cannot decrease Master LC amount to {newAmount.Amount} {newAmount.Currency}. New B2B ceiling ({newCeiling}) is less than active B2B allocation ({CumulativeB2bAllocatedAmount.Amount}).");
            }
        }

        TotalAmount = newAmount;
    }

    public void Close(string makerUserId, string checkerUserId)
    {
        if (Status == MasterLcStatus.Closed)
            throw new InvalidOperationException($"Master LC '{Id}' is already closed.");

        if (string.Equals(makerUserId.Trim(), checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "MakerCheckerSeparationInvariant",
                "BRPD Circular 12/2024 (1.3.18, 10.3.5-x)",
                "Maker cannot act as Checker to close Master Export LC.");
        }

        if (CumulativeB2bAllocatedAmount.Amount > 0m)
        {
            throw new InvariantViolationException(
                "MasterLcCannotCloseWithActiveB2bInvariant",
                "BRPD Circular 12/2024 (5.6.1-viii)",
                $"Cannot close Master Export LC '{Id}' because active Back-to-Back LCs totaling {CumulativeB2bAllocatedAmount.Amount} {CumulativeB2bAllocatedAmount.Currency} remain open.");
        }

        Status = MasterLcStatus.Closed;
    }
}
