using CBS.Domain.BranchCash;
using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.Lending.Agricultural;
using CBS.Domain.Lending.Discounted;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Specialized;

public interface ISpecializedBankingRepository
{
    // 1. Agricultural Credit
    Task<AgriculturalLoanFacility?> GetAgriLoanAsync(AccountNumber accountNumber, CancellationToken ct = default);
    Task AddAgriLoanAsync(AgriculturalLoanFacility facility, CancellationToken ct = default);
    Task<IReadOnlyList<AgriculturalLoanFacility>> ListAgriLoansByCifAsync(CifNumber cif, CancellationToken ct = default);

    // 2. Bill Discounting & IBP
    Task<BillDiscountingFacility?> GetBillDiscountingAsync(AccountNumber accountNumber, CancellationToken ct = default);
    Task<BillDiscountingFacility?> GetBillByReferenceAsync(string billReference, CancellationToken ct = default);
    Task AddBillDiscountingAsync(BillDiscountingFacility facility, CancellationToken ct = default);

    // 3. Deposit Multiplication Schemes
    Task<DepositMultiplicationAccount?> GetMultiplicationAccountAsync(AccountNumber accountNumber, CancellationToken ct = default);
    Task AddMultiplicationAccountAsync(DepositMultiplicationAccount account, CancellationToken ct = default);

    // 4. Inter-Branch Payment Instruments (DD & TT)
    Task<InterBranchPaymentInstrument?> GetInstrumentByNumberAsync(string instrumentNumber, CancellationToken ct = default);
    Task AddInstrumentAsync(InterBranchPaymentInstrument instrument, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
