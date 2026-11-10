using CBS.Application.Common.Interfaces;
using CBS.Domain.AiIntelligence;
using CBS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class EfAiKnowledgeRepository : IAiKnowledgeRepository
{
    private readonly CbsDbContext _dbContext;

    private static readonly List<RegulatoryKnowledgeDocument> DefaultDocuments = new()
    {
        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-1",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 1.3 & 10.3.5",
            title: "Core Accounting Double-Entry & Zero Financial Mutation Invariant",
            content: "All vouchers must self-balance (Sum Dr = Sum Cr). Direct financial ledger mutation or balance alteration by AI assistants or automated unvetted scripts is strictly prohibited.",
            tags: new[] { "voucher", "double-entry", "mutation", "accounting", "ledger" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-3",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 3.1 & 3.10",
            title: "General Banking Dormancy & Walk-In Customer Due Diligence",
            content: "Savings accounts become dormant after 12 months and Current accounts after 6 months of customer-induced inactivity. Cash transactions of BDT 50,000 or above for non-account walk-in customers require mandatory NID, phone, and purpose verification.",
            tags: new[] { "dormancy", "walk-in", "cdd", "savings", "current", "cash" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-4",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 4.1 & 4.3",
            title: "Credit Administration (CAD) & Islamic Financing Shariah Directives",
            content: "Three-tier segregation must be maintained: CRM approves, CAD verifies documentation/collateral and loads limits, and branch disburses. Bai-Murabaha disbursement must be direct to verified goods vendors via Pay Order/RTGS. Quard-e-Hasana contracts must charge strictly 0.00% interest.",
            tags: new[] { "cad", "loan", "lending", "murabaha", "quard", "islamic", "collateral" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-5",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 5.1 & 5.4",
            title: "Trade Finance Documentary Credit, Shipping Guarantee & TBML Variance",
            content: "LC issuance requires pre-sanction screening and valid IRC. Shipping Guarantees require 100% cash margin lock and release strictly on original BL surrender. Unit price variances exceeding +/- 15% against global benchmarks trigger TBML price anomaly alerts.",
            tags: new[] { "trade", "lc", "shipping guarantee", "tbml", "under-invoicing", "over-invoicing", "export" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-6",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 6.2 & 6.5",
            title: "Treasury Deal Segregation & Statutory Reserves (CRR / SLR / ALM)",
            content: "Treasury mandates Front-Office dealer, Mid-Office risk officer, and Back-Office settlement segregation. CRR daily minimum is 3.50% (bi-weekly average 4.00%). SLR is 13.00% for Conventional and 5.50% for Islamic banks. Negative liquidity gaps in 1-14 days bucket exceeding 10% trigger prudential alerts.",
            tags: new[] { "treasury", "crr", "slr", "alm", "liquidity", "fx", "dealer" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-10",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 10.3.5 & 10.3.6",
            title: "Information Security, Super/Admin Financial Prohibition & Cryptographic Audit Chains",
            content: "System Administrators are strictly prohibited from performing financial transactions. Concurrent sessions by single users are blocked. Audit logs must be linked with SHA-256 cryptographic hash chains for tamper-evident CAATs verification.",
            tags: new[] { "security", "admin", "audit", "hash chain", "session", "caats", "bfiu" }),

        new RegulatoryKnowledgeDocument(
            documentId: "BRPD-12-MOD-11",
            circularNo: "BRPD Circular No. 12 (13 May 2024)",
            moduleReference: "Module 11.1 & 11.2",
            title: "BFIU Regulatory Compliance Reporting (CTR and STR/SAR)",
            content: "Daily Cash Transaction Reports (CTR) are mandatory for cumulative daily customer cash transactions of BDT 1,000,000 or above. Suspicious Transaction Reports (STR) must be filed for transaction structuring (smurfing) and TP limit breaches.",
            tags: new[] { "bfiu", "ctr", "str", "aml", "cft", "structuring", "smurfing" })
    };

    public EfAiKnowledgeRepository(CbsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<RegulatoryKnowledgeDocument>> SearchKnowledgeBaseAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        // Ensure initial documents exist in database
        await EnsureSeededAsync(cancellationToken);

        var docs = await _dbContext.RegulatoryKnowledgeDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            return docs.Take(topK).ToList();
        }

        var terms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '-', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();

        var scored = docs.Select(doc =>
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

        return scored;
    }

    public async Task<IReadOnlyList<RegulatoryKnowledgeDocument>> GetAllKnowledgeDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        return await _dbContext.RegulatoryKnowledgeDocuments
            .AsNoTracking()
            .OrderBy(d => d.DocumentId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAdvisoryQueryAsync(AiAdvisoryQuery query, CancellationToken cancellationToken = default)
    {
        await _dbContext.AiAdvisoryQueries.AddAsync(query, cancellationToken);
    }

    public async Task<AiAdvisoryQuery?> GetAdvisoryQueryByIdAsync(string queryId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AiAdvisoryQueries
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == queryId, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAdvisoryQuery>> GetAllAdvisoryQueriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.AiAdvisoryQueries
            .AsNoTracking()
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveTbmlAssessmentAsync(TbmlSemanticAssessment assessment, CancellationToken cancellationToken = default)
    {
        await _dbContext.TbmlSemanticAssessments.AddAsync(assessment, cancellationToken);
    }

    public async Task<TbmlSemanticAssessment?> GetTbmlAssessmentByIdAsync(string assessmentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TbmlSemanticAssessments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssessmentId == assessmentId, cancellationToken);
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        bool hasAny = await _dbContext.RegulatoryKnowledgeDocuments.AnyAsync(cancellationToken);
        if (!hasAny)
        {
            await _dbContext.RegulatoryKnowledgeDocuments.AddRangeAsync(DefaultDocuments, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
