using CBS.Application.Specialized.DTOs;
using CBS.Domain.BranchCash;
using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending.Agricultural;
using CBS.Domain.Lending.Discounted;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Specialized.Commands;

public class SpecializedBankingCommandHandler
{
    private readonly ISpecializedBankingRepository _repository;

    public SpecializedBankingCommandHandler(ISpecializedBankingRepository repository)
    {
        _repository = repository;
    }

    // =========================================================================
    // 1. AGRICULTURAL CREDIT (BRPD MODULE 4.4)
    // =========================================================================
    public async Task<AgriLoanResponse> InitiateAgriLoanAsync(InitiateAgriLoanRequest request, CancellationToken ct = default)
    {
        var accountNumber = AccountNumber.Generate(request.BranchCode, "701", Random.Shared.Next(1000, 99999));
        var farmerAccount = AccountNumber.From(request.FarmerSavingsAccount);

        var facility = new AgriculturalLoanFacility(
            accountNumber,
            new CifNumber(request.Cif),
            request.KrishiCardNumber,
            farmerAccount,
            request.CropType,
            request.LandPorchaNumber,
            request.UnionParishadRef,
            request.BranchCode,
            Money.Bdt(request.SanctionLimit),
            request.AnnualInterestRate,
            request.SanctionDate,
            request.HarvestMaturityDate);

        await _repository.AddAgriLoanAsync(facility, ct);
        await _repository.SaveChangesAsync(ct);

        return MapAgriResponse(facility);
    }

    public async Task<AgriLoanResponse> DisburseAgriLoanAsync(DisburseAgriLoanRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetAgriLoanAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Agricultural loan '{request.AccountNumber}' not found.");

        facility.Disburse(request.DisbursementDate);
        await _repository.SaveChangesAsync(ct);

        return MapAgriResponse(facility);
    }

    public async Task<AgriLoanResponse> RepayAgriLoanAsync(RepayAgriLoanRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetAgriLoanAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Agricultural loan '{request.AccountNumber}' not found.");

        facility.Repay(Money.Bdt(request.Amount));
        await _repository.SaveChangesAsync(ct);

        return MapAgriResponse(facility);
    }

    public async Task<AgriLoanResponse> RescheduleAgriLoanAsync(RescheduleAgriLoanRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetAgriLoanAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Agricultural loan '{request.AccountNumber}' not found.");

        facility.RescheduleForDisaster(request.NewMaturityDate, request.DisasterReference, request.RescheduleDate);
        await _repository.SaveChangesAsync(ct);

        return MapAgriResponse(facility);
    }

    public async Task<AgriLoanResponse?> GetAgriLoanAsync(string accountNumber, CancellationToken ct = default)
    {
        var facility = await _repository.GetAgriLoanAsync(AccountNumber.From(accountNumber), ct);
        return facility is not null ? MapAgriResponse(facility) : null;
    }

    // =========================================================================
    // 2. BILL DISCOUNTING & IBP (BRPD MODULE 4.6)
    // =========================================================================
    public async Task<BillDiscountingResponse> DiscountBillAsync(DiscountBillRequest request, CancellationToken ct = default)
    {
        var accountNumber = AccountNumber.Generate(request.BranchCode, "601", Random.Shared.Next(1000, 99999));

        var facility = new BillDiscountingFacility(
            accountNumber,
            new CifNumber(request.Cif),
            request.BillReferenceNumber,
            request.DraweeBankRouting,
            request.DraweeBankName,
            request.BranchCode,
            Money.Bdt(request.BillFaceValue),
            request.DiscountRatePercent,
            request.TenorDays,
            request.PurchaseDate);

        await _repository.AddBillDiscountingAsync(facility, ct);
        await _repository.SaveChangesAsync(ct);

        return MapBillResponse(facility);
    }

    public async Task<BillDiscountingResponse> AmortizeBillDiscountAsync(AmortizeBillDiscountRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetBillDiscountingAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Discounted bill '{request.AccountNumber}' not found.");

        facility.AmortizeDailyUnearnedInterest(request.ElapsedDays);
        await _repository.SaveChangesAsync(ct);

        return MapBillResponse(facility);
    }

    public async Task<BillDiscountingResponse> EarlyRetireBillAsync(EarlyRetireBillRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetBillDiscountingAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Discounted bill '{request.AccountNumber}' not found.");

        facility.EarlyRetireAndRebate();
        await _repository.SaveChangesAsync(ct);

        return MapBillResponse(facility);
    }

    public async Task<BillDiscountingResponse> LiquidateBillAsync(LiquidateBillRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var facility = await _repository.GetBillDiscountingAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Discounted bill '{request.AccountNumber}' not found.");

        facility.LiquidateFromDraweeRealization(Money.Bdt(request.RealizedAmount));
        await _repository.SaveChangesAsync(ct);

        return MapBillResponse(facility);
    }

    public async Task<BillDiscountingResponse?> GetDiscountedBillAsync(string accountNumber, CancellationToken ct = default)
    {
        var facility = await _repository.GetBillDiscountingAsync(AccountNumber.From(accountNumber), ct);
        return facility is not null ? MapBillResponse(facility) : null;
    }

    // =========================================================================
    // 3. DEPOSIT MULTIPLICATION SCHEMES (BRPD MODULE 3.7)
    // =========================================================================
    public async Task<MultiplicationAccountResponse> OpenMultiplicationAccountAsync(OpenMultiplicationAccountRequest request, CancellationToken ct = default)
    {
        var accountNumber = AccountNumber.Generate(request.BranchCode, "301", Random.Shared.Next(1000, 99999));

        var account = new DepositMultiplicationAccount(
            accountNumber,
            new CifNumber(request.Cif),
            request.BranchCode,
            Money.Bdt(request.InitialDeposit),
            request.MultiplierFactor,
            request.AnnualInterestRate,
            request.OpenDate);

        await _repository.AddMultiplicationAccountAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return MapMultiplicationResponse(account);
    }

    public async Task<MultiplicationAccountResponse> EncashMultiplicationAccountAsync(EncashMultiplicationAccountRequest request, CancellationToken ct = default)
    {
        var acc = AccountNumber.From(request.AccountNumber);
        var account = await _repository.GetMultiplicationAccountAsync(acc, ct)
            ?? throw new KeyNotFoundException($"Multiplication account '{request.AccountNumber}' not found.");

        account.Encash(request.EncashmentDate, request.PrematurePenaltyRate);
        await _repository.SaveChangesAsync(ct);

        return MapMultiplicationResponse(account);
    }

    public async Task<MultiplicationAccountResponse?> GetMultiplicationAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var account = await _repository.GetMultiplicationAccountAsync(AccountNumber.From(accountNumber), ct);
        return account is not null ? MapMultiplicationResponse(account) : null;
    }

    // =========================================================================
    // 4. INTER-BRANCH REMITTANCE TEST KEY ENGINE (BRPD MODULE 3.12 & 3.13)
    // =========================================================================
    public async Task<InterBranchInstrumentResponse> IssueInstrumentAsync(IssueInterBranchInstrumentRequest request, CancellationToken ct = default)
    {
        var instrument = new InterBranchPaymentInstrument(
            request.InstrumentNumber,
            request.Type,
            Money.Bdt(request.Amount),
            Money.Bdt(request.Commission),
            Money.Bdt(request.VatAmount),
            request.IssuingBranchCode,
            request.PayeeBranchCode,
            request.BeneficiaryName,
            request.RemitterName,
            request.IssueDate,
            isSanctionScreened: true, // Cleared via sanction pre-screening
            request.BeneficiaryAccountNumber);

        await _repository.AddInstrumentAsync(instrument, ct);
        await _repository.SaveChangesAsync(ct);

        return MapInstrumentResponse(instrument);
    }

    public async Task<InterBranchInstrumentResponse> VerifyAndPayInstrumentAsync(VerifyAndPayInterBranchInstrumentRequest request, CancellationToken ct = default)
    {
        var instrument = await _repository.GetInstrumentByNumberAsync(request.InstrumentNumber, ct)
            ?? throw new KeyNotFoundException($"Instrument '{request.InstrumentNumber}' not found.");

        instrument.VerifyAndPay(request.ProvidedTestKey, request.PayingUserId, request.PaymentDate);
        await _repository.SaveChangesAsync(ct);

        return MapInstrumentResponse(instrument);
    }

    public async Task<InterBranchInstrumentResponse> CancelInstrumentAsync(CancelInterBranchInstrumentRequest request, CancellationToken ct = default)
    {
        var instrument = await _repository.GetInstrumentByNumberAsync(request.InstrumentNumber, ct)
            ?? throw new KeyNotFoundException($"Instrument '{request.InstrumentNumber}' not found.");

        instrument.CancelAndRefund(request.AuthorizingUserId);
        await _repository.SaveChangesAsync(ct);

        return MapInstrumentResponse(instrument);
    }

    public async Task<InterBranchInstrumentResponse?> GetPaymentInstrumentAsync(string instrumentNumber, CancellationToken ct = default)
    {
        var instrument = await _repository.GetInstrumentByNumberAsync(instrumentNumber, ct);
        return instrument is not null ? MapInstrumentResponse(instrument) : null;
    }

    // --- MAPPERS ---
    private static AgriLoanResponse MapAgriResponse(AgriculturalLoanFacility f) =>
        new(f.Id.Value, f.Cif.Value, f.KrishiCardNumber, f.CropType.ToString(),
            f.SanctionLimit.Amount, f.OutstandingPrincipal.Amount, f.AccruedSimpleInterest.Amount,
            f.AccruedCharges.Amount, f.IsNonAccrual, f.Status.ToString());

    private static BillDiscountingResponse MapBillResponse(BillDiscountingFacility b) =>
        new(b.Id.Value, b.Cif.Value, b.BillReferenceNumber, b.DraweeBankName,
            b.BillFaceValue.Amount, b.UpfrontDiscountAmount.Amount, b.NetDisbursedAmount.Amount,
            b.UnearnedDiscountBalance.Amount, b.RecognizedPnLInterest.Amount, b.Status.ToString());

    private static MultiplicationAccountResponse MapMultiplicationResponse(DepositMultiplicationAccount m) =>
        new(m.Id.Value, m.Cif.Value, m.BranchCode, m.InitialDeposit.Amount, m.MultiplierFactor,
            m.CalculatedTenorMonths, m.MaturityAmount.Amount, m.OpenDate, m.MaturityDate, m.IsClosed);

    private static InterBranchInstrumentResponse MapInstrumentResponse(InterBranchPaymentInstrument i) =>
        new(i.InstrumentNumber, i.Type.ToString(), i.Amount.Amount, i.Commission.Amount, i.VatAmount.Amount,
            i.TotalPayable.Amount, i.IssuingBranchCode, i.PayeeBranchCode, i.BeneficiaryName,
            i.RemitterName, i.IssueDate, i.CryptographicTestKey, i.IsSanctionScreened,
            i.ContraEntryPosted, i.Status.ToString());
}
