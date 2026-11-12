using CBS.Application.Accounts.DTOs;

namespace CBS.Application.Common.Interfaces;

public interface IAccountStatementService
{
    Task<AccountStatementDto> GenerateStatementAsync(GenerateStatementRequest request, CancellationToken cancellationToken = default);
    Task<BalanceConfirmationCertificateDto> GenerateBalanceCertificateAsync(GenerateBalanceCertificateRequest request, CancellationToken cancellationToken = default);
    Task<TaxDeductionCertificateDto> GenerateTaxCertificateAsync(GenerateTaxCertificateRequest request, CancellationToken cancellationToken = default);
}
