using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Customers;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers.Commands.CreateCustomerDraft;

public class CreateCustomerDraftCommandHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISanctionScreeningService _sanctionService;

    public CreateCustomerDraftCommandHandler(
        ICustomerRepository customerRepository,
        ISanctionScreeningService sanctionService)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _sanctionService = sanctionService ?? throw new ArgumentNullException(nameof(sanctionService));
    }

    public async Task<CustomerResponseDto> HandleAsync(CreateCustomerDraftCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        var req = command.Request;

        // 1. Mandatory Maker Attribute
        if (string.IsNullOrWhiteSpace(command.MakerUserId))
            throw new ArgumentException("Maker User ID is mandatory for customer onboarding (BRPD 1.3.18).", nameof(command));

        // 2. Validate Value Objects
        NationalIdentityNumber? nid = !string.IsNullOrWhiteSpace(req.NationalId)
            ? new NationalIdentityNumber(req.NationalId)
            : null;

        TaxIdentificationNumber? tin = !string.IsNullOrWhiteSpace(req.TaxId)
            ? new TaxIdentificationNumber(req.TaxId)
            : null;

        var presentAddress = new Address(req.StreetAddress, req.ThanaOrUpazila, req.District, req.PostalCode);
        var permanentAddress = presentAddress; // Default to present if not separate

        CifNumber? legalGuardianCif = !string.IsNullOrWhiteSpace(req.LegalGuardianCif)
            ? new CifNumber(req.LegalGuardianCif)
            : null;

        // 3. Deduplication Check (BRPD 3.1.15-iii, 8.1.1-d)
        if (nid is not null)
        {
            var existingByNid = await _customerRepository.GetByNationalIdAsync(nid, cancellationToken);
            if (existingByNid is not null)
            {
                throw new InvariantViolationException(
                    "CustomerDeduplicationInvariant",
                    "BRPD Circular 12/2024 (3.1.15-iii, 8.1.1-d)",
                    $"Customer already exists with NID '{nid.Value}' under CIF '{existingByNid.Id.Value}'. Duplicate CIF onboarding prohibited.");
            }
        }

        // 4. Pre-onboarding Sanctions Screening (BRPD 8.1.14, 5.0)
        var sanctionResult = await _sanctionService.ScreenAsync(
            new SanctionScreeningRequest(req.FullName, req.DateOfBirth, nid, presentAddress.CountryCode),
            cancellationToken);

        if (sanctionResult.Status == SanctionStatus.ConfirmedSanctionHit)
        {
            throw new InvariantViolationException(
                "SanctionScreeningHitInvariant",
                "BRPD Circular 12/2024 (8.1.14, 5.0)",
                $"Customer '{req.FullName}' is confirmed in UNSCR/CTB sanction list ({sanctionResult.MatchedListReference}). Onboarding blocked.");
        }

        // 5. Generate Next Unique CIF
        var nextCif = new CifNumber($"CIF{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1000, 9999)}");

        // 6. Instantiate Domain Customer Aggregate (Enforces Minor, Risk, and Age Invariants)
        var customer = new Customer(
            nextCif,
            req.Type,
            req.FullName,
            req.DateOfBirth,
            req.Gender,
            req.FatherName,
            req.MotherName,
            presentAddress,
            permanentAddress,
            req.MobileNumber,
            req.SectorCode,
            nationalId: nid,
            legalGuardianCif: legalGuardianCif,
            pepStatus: req.PepStatus,
            sanctionStatus: sanctionResult.Status,
            fatcaStatus: req.FatcaStatus);

        // 7. Submit For Checker Approval (Maker-Checker Segregation BRPD 1.3.18)
        customer.SubmitForApproval(command.MakerUserId);

        // 8. Persist Draft
        await _customerRepository.AddAsync(customer, cancellationToken);

        return new CustomerResponseDto(
            customer.Id.Value,
            customer.FullName,
            customer.Type.ToString(),
            customer.RiskLevel.ToString(),
            customer.IsActive,
            customer.IsElectionCommissionVerified,
            customer.ComputeDeduplicationFingerprint(),
            customer.NationalId?.Value,
            customer.LegalGuardianCif?.Value);
    }
}
