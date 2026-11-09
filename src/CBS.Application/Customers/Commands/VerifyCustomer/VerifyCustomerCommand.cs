namespace CBS.Application.Customers.Commands.VerifyCustomer;

public record VerifyCustomerCommand(
    string CifNumber,
    string CheckerUserId,
    bool IsApproved,
    string? RejectionReason = null);
