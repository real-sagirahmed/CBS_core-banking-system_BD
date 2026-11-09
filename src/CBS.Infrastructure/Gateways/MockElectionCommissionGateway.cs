using CBS.Application.Common.Interfaces;

namespace CBS.Infrastructure.Gateways;

public class MockElectionCommissionGateway : IElectionCommissionGateway
{
    public static readonly string KnownValidSmartNid = "1234567890";
    public static readonly string KnownUnmatchedNid = "9999999999";

    public Task<EcVerificationResponse> VerifyAsync(EcVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.NationalId.Value == KnownUnmatchedNid)
        {
            return Task.FromResult(new EcVerificationResponse(
                IsMatched: false,
                CitizenNameBengali: null,
                CitizenNameEnglish: null,
                FatherName: null,
                MotherName: null,
                PermanentAddress: null,
                VerificationToken: null,
                ErrorMessage: "Citizen record not found in Election Commission database."));
        }

        return Task.FromResult(new EcVerificationResponse(
            IsMatched: true,
            CitizenNameBengali: "মোহাম্মদ রহিম",
            CitizenNameEnglish: request.FullNameEnglish,
            FatherName: "Abdul Karim",
            MotherName: "Rahima Begum",
            PermanentAddress: "House 12, Road 5, Dhanmondi, Dhaka - 1209, BGD",
            VerificationToken: $"EC_TOKEN_{Guid.NewGuid():N}",
            ErrorMessage: null));
    }
}
