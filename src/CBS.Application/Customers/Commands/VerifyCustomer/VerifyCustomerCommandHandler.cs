using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.DTOs;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Customers.Commands.VerifyCustomer;

public class VerifyCustomerCommandHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyCustomerCommandHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CustomerResponseDto> HandleAsync(VerifyCustomerCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        if (string.IsNullOrWhiteSpace(command.CheckerUserId))
            throw new ArgumentException("Checker User ID is mandatory (BRPD 1.3.18, 10.3.5-x).", nameof(command));

        var customer = await _customerRepository.GetByCifAsync(new CifNumber(command.CifNumber), cancellationToken)
            ?? throw new KeyNotFoundException($"Customer with CIF '{command.CifNumber}' not found.");

        if (command.IsApproved)
        {
            customer.Authorize(command.CheckerUserId);
        }
        else
        {
            customer.Reject(command.CheckerUserId, command.RejectionReason ?? "Rejected by Checker without comments.");
        }

        await _customerRepository.UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
