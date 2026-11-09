using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public record SanctionScreeningRequest(
    string FullName,
    DateOnly DateOfBirth,
    NationalIdentityNumber? NationalId,
    string CountryCode);

public record SanctionScreeningResult(
    SanctionStatus Status,
    string? MatchedListReference,
    decimal MatchConfidenceScore,
    string? ScreeningNarrative);

public interface ISanctionScreeningService
{
    Task<SanctionScreeningResult> ScreenAsync(SanctionScreeningRequest request, CancellationToken cancellationToken = default);
}
