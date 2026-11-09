using CBS.Application.Common.Interfaces;
using CBS.Domain.AiIntelligence;
using CBS.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace CBS.Infrastructure.AiIntelligence;

public class InMemoryAiKnowledgeRepository : IAiKnowledgeRepository
{
    private readonly List<RegulatoryKnowledgeDocument> _knowledgeBase = new();
    private readonly ConcurrentDictionary<string, AiAdvisoryQuery> _queries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TbmlSemanticAssessment> _tbmlAssessments = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryAiKnowledgeRepository()
    {
        SeedKnowledgeBase();
    }

    private void SeedKnowledgeBase()
    {
        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-1",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 1.3 & 10.3.5",
            Title: "Core Accounting Double-Entry & Zero Financial Mutation Invariant",
            Content: "All vouchers must self-balance (Sum Dr = Sum Cr). Direct financial ledger mutation or balance alteration by AI assistants or automated unvetted scripts is strictly prohibited.",
            Tags: new[] { "voucher", "double-entry", "mutation", "accounting", "ledger" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-3",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 3.1 & 3.10",
            Title: "General Banking Dormancy & Walk-In Customer Due Diligence",
            Content: "Savings accounts become dormant after 12 months and Current accounts after 6 months of customer-induced inactivity. Cash transactions of BDT 50,000 or above for non-account walk-in customers require mandatory NID, phone, and purpose verification.",
            Tags: new[] { "dormancy", "walk-in", "cdd", "savings", "current", "cash" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-4",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 4.1 & 4.3",
            Title: "Credit Administration (CAD) & Islamic Financing Shariah Directives",
            Content: "Three-tier segregation must be maintained: CRM approves, CAD verifies documentation/collateral and loads limits, and branch disburses. Bai-Murabaha disbursement must be direct to verified goods vendors via Pay Order/RTGS. Quard-e-Hasana contracts must charge strictly 0.00% interest.",
            Tags: new[] { "cad", "loan", "lending", "murabaha", "quard", "islamic", "collateral" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-5",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 5.1 & 5.4",
            Title: "Trade Finance Documentary Credit, Shipping Guarantee & TBML Variance",
            Content: "LC issuance requires pre-sanction screening and valid IRC. Shipping Guarantees require 100% cash margin lock and release strictly on original BL surrender. Unit price variances exceeding +/- 15% against global benchmarks trigger TBML price anomaly alerts.",
            Tags: new[] { "trade", "lc", "shipping guarantee", "tbml", "under-invoicing", "over-invoicing", "export" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-6",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 6.2 & 6.5",
            Title: "Treasury Deal Segregation & Statutory Reserves (CRR / SLR / ALM)",
            Content: "Treasury mandates Front-Office dealer, Mid-Office risk officer, and Back-Office settlement segregation. CRR daily minimum is 3.50% (bi-weekly average 4.00%). SLR is 13.00% for Conventional and 5.50% for Islamic banks. Negative liquidity gaps in 1-14 days bucket exceeding 10% trigger prudential alerts.",
            Tags: new[] { "treasury", "crr", "slr", "alm", "liquidity", "fx", "dealer" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-10",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 10.3.5 & 10.3.6",
            Title: "Information Security, Super/Admin Financial Prohibition & Cryptographic Audit Chains",
            Content: "System Administrators are strictly prohibited from performing financial transactions. Concurrent sessions by single users are blocked. Audit logs must be linked with SHA-256 cryptographic hash chains for tamper-evident CAATs verification.",
            Tags: new[] { "security", "admin", "audit", "hash chain", "session", "caats", "bfiu" }));

        _knowledgeBase.Add(new RegulatoryKnowledgeDocument(
            DocumentId: "BRPD-12-MOD-11",
            CircularNo: "BRPD Circular No. 12 (13 May 2024)",
            ModuleReference: "Module 11.1 & 11.2",
            Title: "BFIU Regulatory Compliance Reporting (CTR and STR/SAR)",
            Content: "Daily Cash Transaction Reports (CTR) are mandatory for cumulative daily customer cash transactions of BDT 1,000,000 or above. Suspicious Transaction Reports (STR) must be filed for transaction structuring (smurfing) and TP limit breaches.",
            Tags: new[] { "bfiu", "ctr", "str", "aml", "cft", "structuring", "smurfing" }));
    }

    public Task<IReadOnlyList<RegulatoryKnowledgeDocument>> SearchKnowledgeBaseAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<RegulatoryKnowledgeDocument>>(_knowledgeBase.Take(topK).ToList());
        }

        var terms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '-', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();

        var scored = _knowledgeBase.Select(doc =>
        {
            int score = 0;
            var docContent = (doc.Title + " " + doc.Content + " " + string.Join(" ", doc.Tags)).ToLowerInvariant();

            foreach (var term in terms)
            {
                if (doc.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase))) score += 5;
                if (doc.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 3;
                if (docContent.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 1;
            }

            return new { Doc = doc, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Take(topK)
        .Select(x => x.Doc)
        .ToList();

        return Task.FromResult<IReadOnlyList<RegulatoryKnowledgeDocument>>(scored);
    }

    public Task SaveAdvisoryQueryAsync(AiAdvisoryQuery query, CancellationToken cancellationToken = default)
    {
        _queries[query.QueryId] = query;
        return Task.CompletedTask;
    }

    public Task<AiAdvisoryQuery?> GetAdvisoryQueryByIdAsync(string queryId, CancellationToken cancellationToken = default)
    {
        _queries.TryGetValue(queryId, out var query);
        return Task.FromResult(query);
    }

    public Task<IReadOnlyList<AiAdvisoryQuery>> GetAllAdvisoryQueriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AiAdvisoryQuery> list = _queries.Values.ToList();
        return Task.FromResult(list);
    }

    public Task SaveTbmlAssessmentAsync(TbmlSemanticAssessment assessment, CancellationToken cancellationToken = default)
    {
        _tbmlAssessments[assessment.AssessmentId] = assessment;
        return Task.CompletedTask;
    }

    public Task<TbmlSemanticAssessment?> GetTbmlAssessmentByIdAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        _tbmlAssessments.TryGetValue(assessmentId, out var assessment);
        return Task.FromResult(assessment);
    }
}
