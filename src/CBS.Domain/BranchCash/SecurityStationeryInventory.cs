using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;

namespace CBS.Domain.BranchCash;

// ARCHITECTURAL INTENT: Joint-custody security stationery inventory management (BRPD Circular 12/2024 Module 3.11.1-iv).
// Unissued leaf ranges must be held under dual control (Manager + Custodian) to prevent unauthorized instrument issuance.
public class SecurityStationeryInventory : AggregateRoot<Guid>
{
    public string BranchCode { get; private set; }
    public RemittanceInstrumentType InstrumentType { get; private set; }
    public string BookPrefix { get; private set; }
    public long StartLeaf { get; private set; }
    public long EndLeaf { get; private set; }
    public long CurrentLeaf { get; private set; }
    public string ManagerUserId { get; private set; }
    public string CustodianUserId { get; private set; }
    public bool IsActive { get; private set; }

    public SecurityStationeryInventory(
        Guid id,
        string branchCode,
        RemittanceInstrumentType instrumentType,
        string bookPrefix,
        long startLeaf,
        long endLeaf,
        string managerUserId,
        string custodianUserId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(bookPrefix)) throw new ArgumentException("Book prefix is required.", nameof(bookPrefix));
        if (startLeaf <= 0) throw new ArgumentOutOfRangeException(nameof(startLeaf), "Start leaf must be positive.");
        if (endLeaf < startLeaf) throw new ArgumentOutOfRangeException(nameof(endLeaf), "End leaf cannot be less than start leaf.");
        if (string.IsNullOrWhiteSpace(managerUserId)) throw new ArgumentException("Manager User ID is required.", nameof(managerUserId));
        if (string.IsNullOrWhiteSpace(custodianUserId)) throw new ArgumentException("Custodian User ID is required.", nameof(custodianUserId));

        if (managerUserId.Equals(custodianUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "JointCustodySegregationInvariant",
                "BRPD Circular 12/2024 (3.11.1-iv, 10.3.5-x)",
                $"Security stationery cannot be held under single control. Manager '{managerUserId}' and Custodian '{custodianUserId}' must be distinct.");
        }

        BranchCode = branchCode.Trim();
        InstrumentType = instrumentType;
        BookPrefix = bookPrefix.Trim().ToUpperInvariant();
        StartLeaf = startLeaf;
        EndLeaf = endLeaf;
        CurrentLeaf = startLeaf;
        ManagerUserId = managerUserId.Trim();
        CustodianUserId = custodianUserId.Trim();
        IsActive = true;
    }

    public string AllocateNextLeaf()
    {
        if (!IsActive)
        {
            throw new InvariantViolationException(
                "InactiveStationeryBookInvariant",
                "BRPD Circular 12/2024 (3.11.1-iv)",
                $"Stationery book '{BookPrefix}' for branch '{BranchCode}' is not active.");
        }

        if (CurrentLeaf > EndLeaf)
        {
            throw new InvariantViolationException(
                "StationeryExhaustedInvariant",
                "BRPD Circular 12/2024 (3.11.1-iv)",
                $"Stationery book '{BookPrefix}' ({StartLeaf} to {EndLeaf}) has been completely exhausted. Allocate a new book under joint custody.");
        }

        string leafNumber = $"{BookPrefix}-{CurrentLeaf:D7}";
        CurrentLeaf++;
        return leafNumber;
    }

    public void Deactivate(string managerUserId)
    {
        if (!ManagerUserId.Equals(managerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "StationeryDeactivationUnauthorized",
                "BRPD Circular 12/2024 (3.11.1-iv)",
                "Only the designated Branch Manager can deactivate a stationery book.");
        }

        IsActive = false;
    }
}
