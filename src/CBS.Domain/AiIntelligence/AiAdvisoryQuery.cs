using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.AiIntelligence;

/// <summary>
/// Aggregate Root representing an AI Advisory Query and Decision-Support Interaction.
/// Adheres strictly to Bangladesh Bank BRPD Circular 12 (13 May 2024).
/// </summary>
public sealed class AiAdvisoryQuery : AggregateRoot<string>
{
    // ARCHITECTURAL INTENT: Zero Financial Ledger Mutation Lock (BRPD 1.3.5, 10.3.5-vii).
    // AI assistants are strictly restricted to read-only diagnostics, regulatory citations, and decision support.
    // Direct transaction posting, balance alteration, or limit modification via AI is strictly prohibited.

    public string QueryId => Id;
    public string OfficerUserId { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public string BranchCode { get; private set; } = null!;
    public AiQueryCategory Category { get; private set; }
    public string SanitizedQueryText { get; private set; } = null!;
    public string AdvisoryResponse { get; private set; } = null!;
    public IReadOnlyList<GroundedCitation> Citations { get; private set; } = new List<GroundedCitation>();
    public decimal ConfidenceScore { get; private set; }
    public bool RequiresMakerCheckerReview { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AiAdvisoryQuery(string queryId) : base(queryId) { }

    public static AiAdvisoryQuery Create(
        string queryId,
        string officerUserId,
        UserRole role,
        string branchCode,
        AiQueryCategory category,
        string rawQueryText,
        string advisoryResponse,
        IEnumerable<GroundedCitation> citations,
        decimal confidenceScore,
        bool requiresMakerCheckerReview = true)
    {
        if (string.IsNullOrWhiteSpace(queryId))
            throw new ArgumentException("Query ID is required.", nameof(queryId));
        if (string.IsNullOrWhiteSpace(officerUserId))
            throw new ArgumentException("Officer User ID is required.", nameof(officerUserId));
        if (string.IsNullOrWhiteSpace(rawQueryText))
            throw new ArgumentException("Query text is required.", nameof(rawQueryText));

        // Invariant 1: Assert Zero Financial Mutation Lock (BRPD 1.3.5, 10.3.5-vii)
        AssertZeroFinancialMutation(rawQueryText);

        // Invariant 2: Client-side Deterministic PII Redaction (BRPD 9.1.1-xxv, 10.1.3)
        var sanitized = PiiSanitizer.SanitizePrompt(rawQueryText);

        return new AiAdvisoryQuery(queryId.Trim())
        {
            OfficerUserId = officerUserId.Trim(),
            Role = role,
            BranchCode = branchCode.Trim(),
            Category = category,
            SanitizedQueryText = sanitized,
            AdvisoryResponse = advisoryResponse.Trim(),
            Citations = citations.ToList().AsReadOnly(),
            ConfidenceScore = Math.Clamp(confidenceScore, 0.0m, 1.0m),
            RequiresMakerCheckerReview = requiresMakerCheckerReview,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static void AssertZeroFinancialMutation(string queryText)
    {
        var lower = queryText.ToLowerInvariant();
        string[] forbiddenPatterns =
        {
            "transfer money",
            "post voucher",
            "debit account",
            "credit account",
            "disburse loan",
            "disburse fund",
            "execute payment",
            "mutate balance",
            "override balance",
            "withdraw cash",
            "deposit cash"
        };

        foreach (var pattern in forbiddenPatterns)
        {
            if (lower.Contains(pattern))
            {
                throw new InvalidOperationException($"Security Invariant Violation [BRPD 1.3.5, 10.3.5-vii]: AI Assistants are strictly prohibited from mutating financial ledgers or executing transactions. Detected forbidden intent: '{pattern}'.");
            }
        }
    }
}
