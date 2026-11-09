using CBS.Application.Customers.DTOs;

namespace CBS.Application.Customers.Commands.CreateCustomerDraft;

public record CreateCustomerDraftCommand(
    CreateCustomerDraftRequest Request,
    string MakerUserId,
    string BranchCode)
{
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
