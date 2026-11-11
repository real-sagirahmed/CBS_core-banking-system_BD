using CBS.Domain.Exceptions;

namespace CBS.Domain.Security.ValueObjects;

// ARCHITECTURAL INTENT: Delegation of Authority (DOA) Limit Matrix
// Enforces Bangladesh Bank BRPD Circular 12/2024 (10.2.3 & 10.1.5-d)
// Governs local debit, local credit, inter-branch debit/credit, and GL/P&L posting limits.
public sealed record DoaLimitProfile
{
    public string BranchCode { get; init; }
    public string GroupCode { get; init; }
    public decimal MaxLocalDebitAmount { get; init; }
    public decimal MaxLocalCreditAmount { get; init; }
    public decimal MaxInterBranchDebitAmount { get; init; }
    public decimal MaxInterBranchCreditAmount { get; init; }
    public decimal MaxGlPostingAmount { get; init; }

    #pragma warning disable CS8618
    public DoaLimitProfile() { }
    #pragma warning restore CS8618

    public DoaLimitProfile(
        string branchCode,
        string groupCode,
        decimal maxLocalDebitAmount,
        decimal maxLocalCreditAmount,
        decimal maxInterBranchDebitAmount,
        decimal maxInterBranchCreditAmount,
        decimal maxGlPostingAmount)
    {
        if (string.IsNullOrWhiteSpace(branchCode))
            throw new ArgumentException("Branch code is required.", nameof(branchCode));
        if (string.IsNullOrWhiteSpace(groupCode))
            throw new ArgumentException("Group code is required.", nameof(groupCode));

        if (maxLocalDebitAmount < 0 || maxLocalCreditAmount < 0 ||
            maxInterBranchDebitAmount < 0 || maxInterBranchCreditAmount < 0 ||
            maxGlPostingAmount < 0)
        {
            throw new InvariantViolationException(
                "NegativeDoaLimitInvariant",
                "BRPD Circular 12/2024 (10.2.3)",
                "DOA transaction limits cannot be negative.");
        }

        BranchCode = branchCode.Trim();
        GroupCode = groupCode.Trim().ToUpperInvariant();
        MaxLocalDebitAmount = maxLocalDebitAmount;
        MaxLocalCreditAmount = maxLocalCreditAmount;
        MaxInterBranchDebitAmount = maxInterBranchDebitAmount;
        MaxInterBranchCreditAmount = maxInterBranchCreditAmount;
        MaxGlPostingAmount = maxGlPostingAmount;
    }

    public static DoaLimitProfile DefaultTeller(string branchCode) =>
        new(branchCode, "TELLER", 100_000m, 500_000m, 50_000m, 200_000m, 0m);

    public static DoaLimitProfile DefaultSupervisor(string branchCode) =>
        new(branchCode, "SUPERVISOR", 1_000_000m, 5_000_000m, 500_000m, 2_000_000m, 500_000m);

    public static DoaLimitProfile DefaultBranchManager(string branchCode) =>
        new(branchCode, "MANAGER", 10_000_000m, 50_000_000m, 5_000_000m, 20_000_000m, 5_000_000m);

    public static DoaLimitProfile Administrator(string branchCode) =>
        new(branchCode, "ADMIN", 0m, 0m, 0m, 0m, 0m);

    public (bool IsAllowed, decimal UserLimit, string? BreachReason) EvaluateTransaction(
        decimal amount,
        bool isCredit,
        bool isInterBranch,
        bool isGl)
    {
        if (isGl)
        {
            if (amount > MaxGlPostingAmount)
            {
                return (false, MaxGlPostingAmount,
                    $"Transaction amount {amount:N2} BDT exceeds user GL posting limit of {MaxGlPostingAmount:N2} BDT.");
            }
            return (true, MaxGlPostingAmount, null);
        }

        if (isInterBranch)
        {
            decimal limit = isCredit ? MaxInterBranchCreditAmount : MaxInterBranchDebitAmount;
            string dir = isCredit ? "Credit" : "Debit";
            if (amount > limit)
            {
                return (false, limit,
                    $"Inter-branch {dir} amount {amount:N2} BDT exceeds user limit of {limit:N2} BDT.");
            }
            return (true, limit, null);
        }
        else
        {
            decimal limit = isCredit ? MaxLocalCreditAmount : MaxLocalDebitAmount;
            string dir = isCredit ? "Credit" : "Debit";
            if (amount > limit)
            {
                return (false, limit,
                    $"Local {dir} amount {amount:N2} BDT exceeds user limit of {limit:N2} BDT.");
            }
            return (true, limit, null);
        }
    }
}
