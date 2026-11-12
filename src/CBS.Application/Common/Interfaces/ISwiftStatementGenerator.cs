using CBS.Application.Accounts.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface ISwiftStatementGenerator
{
    Task<SwiftStatementResponseDto> GenerateSwiftStatementAsync(GenerateSwiftStatementRequest request, CancellationToken cancellationToken = default);
}
