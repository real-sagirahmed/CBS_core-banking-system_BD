using CBS.Domain.ValueObjects;

namespace CBS.Application.Common.Interfaces;

public record EcVerificationRequest(
    NationalIdentityNumber NationalId,
    DateOnly DateOfBirth,
    string FullNameEnglish,
    byte[]? FaceBiometricImage = null);

public record EcVerificationResponse(
    bool IsMatched,
    string? CitizenNameBengali,
    string? CitizenNameEnglish,
    string? FatherName,
    string? MotherName,
    string? PermanentAddress,
    string? VerificationToken,
    string? ErrorMessage);

public interface IElectionCommissionGateway
{
    Task<EcVerificationResponse> VerifyAsync(EcVerificationRequest request, CancellationToken cancellationToken = default);
}
