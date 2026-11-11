using CBS.Domain.Common;
using CBS.Domain.Customers.ValueObjects;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

// ARCHITECTURAL INTENT: Operational Mandate Resolution Engine for Joint and Corporate Accounts.
// Dynamically verifies whether presenting signatories satisfy the registered legal mandate
// before authorizing any clearing cheque, transfer, or branch withdrawal (BRPD Circular 12/2024 Module 8.1.12-d, e).
public class AccountSignatoryMandate : AggregateRoot<Guid>
{
    public AccountNumber AccountNumber { get; private set; }
    public MandateOperationalRule OperationalRule { get; private set; }
    public decimal SingleSignatoryThresholdBdt { get; private set; }
    public List<MandateSignatoryRule> Signatories { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public string ApprovedByUserId { get; private set; }

    #pragma warning disable CS8618
    protected AccountSignatoryMandate() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public AccountSignatoryMandate(
        Guid id,
        AccountNumber accountNumber,
        MandateOperationalRule operationalRule,
        decimal singleSignatoryThresholdBdt,
        IEnumerable<MandateSignatoryRule> signatories,
        string approvedByUserId,
        DateTime effectiveFromUtc) : base(id)
    {
        ArgumentNullException.ThrowIfNull(accountNumber, nameof(accountNumber));
        if (string.IsNullOrWhiteSpace(approvedByUserId))
            throw new ArgumentException("Approved by user ID is required.", nameof(approvedByUserId));
        if (singleSignatoryThresholdBdt < 0)
            throw new ArgumentException("Single signatory threshold cannot be negative.", nameof(singleSignatoryThresholdBdt));

        var sigList = signatories?.ToList() ?? [];
        if (sigList.Count == 0)
        {
            throw new InvariantViolationException(
                "EmptyMandateSignatoriesInvariant",
                "BRPD Circular 12/2024 (8.1.12-d)",
                "Account operational mandate must contain at least one registered authorized signatory.");
        }

        AccountNumber = accountNumber;
        OperationalRule = operationalRule;
        SingleSignatoryThresholdBdt = singleSignatoryThresholdBdt;
        Signatories = sigList;
        ApprovedByUserId = approvedByUserId.Trim();
        EffectiveFromUtc = effectiveFromUtc;
        IsActive = true;
    }

    /// <summary>
    /// Dynamically evaluates whether the presenting set of signatory IDs satisfies the account mandate.
    /// </summary>
    public (bool IsAuthorized, string Reason) EvaluateMandate(IEnumerable<Guid> presentingSignatoryIds, decimal transactionAmountBdt)
    {
        if (!IsActive)
            return (false, "Mandate is inactive or revoked.");

        var presentingList = presentingSignatoryIds?.Distinct().ToList() ?? [];
        if (presentingList.Count == 0)
            return (false, "No presenting signatures provided for authorization.");

        // Check if all presenting signatures belong to registered authorized signatories
        var validSignatories = Signatories.Where(s => presentingList.Contains(s.SignatoryId)).ToList();
        if (validSignatories.Count < presentingList.Count)
            return (false, "One or more presenting signatures do not match registered authorized signatories.");

        // Threshold override: if amount is below single signatory limit and rule allows single signing
        if (transactionAmountBdt <= SingleSignatoryThresholdBdt && validSignatories.Count >= 1)
        {
            return (true, $"Authorized: Transaction amount {transactionAmountBdt:N2} BDT is within single signatory threshold of {SingleSignatoryThresholdBdt:N2} BDT.");
        }

        switch (OperationalRule)
        {
            case MandateOperationalRule.Singly:
            case MandateOperationalRule.EitherOrSurvivor:
                return validSignatories.Count >= 1
                    ? (true, $"Authorized: Operational rule '{OperationalRule}' satisfied by {validSignatories.Count} valid signature(s).")
                    : (false, "At least one authorized signatory is required.");

            case MandateOperationalRule.JointlyAll:
                bool allPresent = Signatories.All(s => presentingList.Contains(s.SignatoryId));
                return allPresent
                    ? (true, "Authorized: All registered signatories signed jointly.")
                    : (false, $"Mandate 'JointlyAll' requires all {Signatories.Count} signatories, but only {validSignatories.Count} were presented.");

            case MandateOperationalRule.JointlyAnyTwo:
                return validSignatories.Count >= 2
                    ? (true, $"Authorized: Operational rule 'JointlyAnyTwo' satisfied by {validSignatories.Count} signatories.")
                    : (false, $"Mandate 'JointlyAnyTwo' requires at least 2 authorized signatories, but only {validSignatories.Count} was presented.");

            case MandateOperationalRule.ManagingDirectorWithAnyDirector:
                bool hasMd = validSignatories.Any(s => s.Role == StakeholderRole.ManagingDirector || s.IsMandatorySignatory);
                bool hasOtherDirector = validSignatories.Any(s => s.Role == StakeholderRole.Director && !(s.Role == StakeholderRole.ManagingDirector));

                if (hasMd && (hasOtherDirector || validSignatories.Count >= 2))
                {
                    return (true, "Authorized: Managing Director and at least one other Director/Signatory signed.");
                }
                return (false, "Mandate 'ManagingDirectorWithAnyDirector' requires the signature of the Managing Director plus at least one other Director.");

            default:
                return (false, $"Unknown operational mandate rule: {OperationalRule}");
        }
    }

    public void Deactivate(string approvedByUserId)
    {
        IsActive = false;
        ApprovedByUserId = approvedByUserId.Trim();
    }
}
