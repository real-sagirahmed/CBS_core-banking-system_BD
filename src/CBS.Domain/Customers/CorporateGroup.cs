using System;
using System.Collections.Generic;
using CBS.Domain.Common;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Conglomerate Corporate Group Hierarchy & Aggregate Exposure Limits.
// Enforces Bangladesh Bank Single Borrower / Group Exposure Limit across all subsidiary sister concerns
// and BFIU Ultimate Beneficial Owner (UBO) transparency
// (BRPD Circular 12/2024 Module 8.1.1-c, 8.1.12, 8.1.18-d & BRPD Circular No. 01/2022).
public class CorporateGroup : AggregateRoot<string>
{
    public string GroupName { get; private set; }
    public string ParentCifNumber { get; private set; } = string.Empty;
    public List<string> MemberCifNumbers { get; private set; } = [];
    public List<GroupMemberDetail> MemberDetails { get; private set; } = [];
    public List<BeneficialOwnerRecord> BeneficialOwners { get; private set; } = [];
    public decimal StatutoryGroupLimitBdt { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedByUserId { get; private set; }

    #pragma warning disable CS8618
    protected CorporateGroup() : base(string.Empty) { }
    #pragma warning restore CS8618

    public CorporateGroup(
        string id,
        string groupName,
        decimal statutoryGroupLimitBdt,
        string createdByUserId,
        DateTime createdAtUtc,
        string? parentCifNumber = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Group ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(groupName))
            throw new ArgumentException("Group name is required.", nameof(groupName));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required.", nameof(createdByUserId));
        if (statutoryGroupLimitBdt <= 0)
        {
            throw new InvariantViolationException(
                "InvalidGroupLimitInvariant",
                "BRPD Circular 12/2024 (8.1.1-c)",
                "Statutory corporate group exposure limit must be strictly positive.");
        }

        Id = id.Trim().ToUpperInvariant();
        GroupName = groupName.Trim();
        StatutoryGroupLimitBdt = statutoryGroupLimitBdt;
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = createdAtUtc;
        ParentCifNumber = parentCifNumber?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    public void LinkMemberCif(CifNumber cif)
    {
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));

        if (!MemberCifNumbers.Contains(cif.Value))
        {
            MemberCifNumbers.Add(cif.Value);
        }
    }

    public void UnlinkMemberCif(CifNumber cif)
    {
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        MemberCifNumbers.Remove(cif.Value);
        MemberDetails.RemoveAll(m => m.CifNumber == cif.Value);
    }

    public void AddMemberDetail(GroupMemberDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail, nameof(detail));

        if (!MemberCifNumbers.Contains(detail.CifNumber))
        {
            MemberCifNumbers.Add(detail.CifNumber);
        }

        MemberDetails.RemoveAll(m => m.CifNumber == detail.CifNumber);
        MemberDetails.Add(detail);
    }

    public void RegisterBeneficialOwner(BeneficialOwnerRecord ubo)
    {
        ArgumentNullException.ThrowIfNull(ubo, nameof(ubo));

        BeneficialOwners.RemoveAll(b => b.NationalId == ubo.NationalId);
        BeneficialOwners.Add(ubo);
    }

    /// <summary>
    /// Validates whether adding a proposed funded or non-funded credit facility breaches the group limit.
    /// </summary>
    public void ValidateProposedExposure(decimal currentFundedBdt, decimal currentNonFundedBdt, decimal proposedNewFacilityBdt)
    {
        decimal totalProjected = currentFundedBdt + currentNonFundedBdt + proposedNewFacilityBdt;
        if (totalProjected > StatutoryGroupLimitBdt)
        {
            throw new InvariantViolationException(
                "GroupExposureLimitExceededInvariant",
                "BRPD Circular 12/2024 (8.1.1-c, 8.1.18-d)",
                $"Total group exposure of {totalProjected:N2} BDT breaches statutory limit of {StatutoryGroupLimitBdt:N2} BDT for group '{GroupName}'.");
        }
    }
}
