using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;

namespace CBS.Infrastructure.Security;

public class MockSanctionScreeningService : ISanctionScreeningService
{
    private static readonly HashSet<string> KnownSanctionedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Osama", "Terrorist Target", "Sanctioned Individual", "Blocked Entity"
    };

    public Task<SanctionScreeningResult> ScreenAsync(SanctionScreeningRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var sanctioned in KnownSanctionedNames)
        {
            if (request.FullName.Contains(sanctioned, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SanctionScreeningResult(
                    Status: SanctionStatus.ConfirmedSanctionHit,
                    MatchedListReference: "UNSCR 1267 / Bangladesh Anti-Terrorism Act 2009",
                    MatchConfidenceScore: 0.98m,
                    ScreeningNarrative: $"Direct match with prohibited blacklist entry '{sanctioned}'."));
            }
        }

        return Task.FromResult(new SanctionScreeningResult(
            Status: SanctionStatus.Clean,
            MatchedListReference: null,
            MatchConfidenceScore: 0.00m,
            ScreeningNarrative: "Clean. No match found across UNSCR, OFAC, and local CTB databases."));
    }
}
