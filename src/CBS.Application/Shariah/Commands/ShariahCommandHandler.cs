using CBS.Application.Shariah.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Shariah;
using CBS.Domain.Shariah.ValueObjects;

namespace CBS.Application.Shariah.Commands;

public class ShariahCommandHandler
{
    private readonly IShariahComplianceRepository _repository;

    public ShariahCommandHandler(IShariahComplianceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // 1. Mudaraba Investment Pools
    public async Task<MudarabaPoolDto> CreateMudarabaPoolAsync(
        CreateMudarabaPoolRequestDto request,
        CancellationToken ct = default)
    {
        var pool = new MudarabaInvestmentPool(
            request.PoolName,
            request.PoolType,
            request.Currency,
            request.MudaribShareRatio,
            request.PerRatio,
            request.IrrRatio);

        await _repository.SavePoolAsync(pool, ct);
        return MapToDto(pool);
    }

    public async Task<MudarabaPoolDto> ConfigureWeightageAsync(
        Guid poolId,
        ConfigureWeightageRequestDto request,
        CancellationToken ct = default)
    {
        var pool = await _repository.GetPoolByIdAsync(poolId, ct)
            ?? throw new KeyNotFoundException($"Mudaraba pool '{poolId}' not found.");

        var weightage = new MudarabaWeightage(
            request.ProductCode,
            request.ProductName,
            request.Weightage,
            request.DeclaredProvisionalRate);

        pool.SetWeightage(weightage);
        await _repository.SavePoolAsync(pool, ct);

        return MapToDto(pool);
    }

    public async Task<PoolProfitDistributionResultDto> DistributePoolProfitAsync(
        Guid poolId,
        DistributePoolProfitRequestDto request,
        CancellationToken ct = default)
    {
        var pool = await _repository.GetPoolByIdAsync(poolId, ct)
            ?? throw new KeyNotFoundException($"Mudaraba pool '{poolId}' not found.");

        var result = pool.DistributeProfit(request.GrossInvestmentIncome, DateTime.UtcNow);
        await _repository.SavePoolAsync(pool, ct);

        return new PoolProfitDistributionResultDto(
            result.PoolId,
            result.GrossIncome,
            result.MudaribSharePercentage,
            result.MudaribShareAmount,
            result.RabUlMalGrossShare,
            result.PerDeductionAmount,
            result.IrrDeductionAmount,
            result.NetDistributableProfit,
            result.CalculatedAtUtc);
    }

    public async Task<IReadOnlyList<MudarabaPoolDto>> GetPoolsAsync(CancellationToken ct = default)
    {
        await _repository.SeedDefaultIslamicPoolsIfEmptyAsync(ct);
        var pools = await _repository.GetAllPoolsAsync(ct);
        return pools.Select(MapToDto).ToList();
    }

    // 2. Al-Wadiah Current Accounts
    public async Task<AlWadiahAccountDto> OpenAlWadiahAccountAsync(
        OpenAlWadiahAccountRequestDto request,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetAlWadiahByAccountNumberAsync(request.AccountNumber, ct);
        if (existing is not null)
        {
            throw new InvariantViolationException(
                "DuplicateAccountInvariant",
                "BRPD 3.3.1",
                $"Account number '{request.AccountNumber}' already exists.");
        }

        var account = new AlWadiahAccount(
            request.AccountNumber,
            request.Cif,
            request.Currency,
            request.SafeCustodyContractRef);

        await _repository.SaveAlWadiahAccountAsync(account, ct);
        return MapToDto(account);
    }

    public async Task<AlWadiahAccountDto> GetAlWadiahAccountAsync(
        string accountNumber,
        CancellationToken ct = default)
    {
        var account = await _repository.GetAlWadiahByAccountNumberAsync(accountNumber, ct)
            ?? throw new KeyNotFoundException($"Al-Wadiah account '{accountNumber}' not found.");

        return MapToDto(account);
    }

    // 3. Bai-Murabaha Financing Facilities
    public async Task<MurabahaFacilityDto> CreateMurabahaFacilityAsync(
        CreateMurabahaFacilityRequestDto request,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetMurabahaByFacilityNumberAsync(request.FacilityNumber, ct);
        if (existing is not null)
        {
            throw new InvariantViolationException(
                "DuplicateFacilityInvariant",
                "BRPD 4.1.5",
                $"Facility number '{request.FacilityNumber}' already exists.");
        }

        var facility = new MurabahaFinancingFacility(
            request.FacilityNumber,
            request.Cif,
            request.BranchCode,
            request.PurchaseCost,
            request.ProfitMarginAmount,
            request.RepaymentTenorMonths,
            request.GoodsDescription);

        await _repository.SaveMurabahaFacilityAsync(facility, ct);
        return MapToDto(facility);
    }

    public async Task<MurabahaFacilityDto> InspectGoodsAsync(
        Guid facilityId,
        InspectGoodsRequestDto request,
        CancellationToken ct = default)
    {
        var facility = await _repository.GetMurabahaByIdAsync(facilityId, ct)
            ?? throw new KeyNotFoundException($"Murabaha facility '{facilityId}' not found.");

        facility.RecordGoodsInspection(request.InspectionProofReference, request.InspectorOfficerId);
        await _repository.SaveMurabahaFacilityAsync(facility, ct);

        return MapToDto(facility);
    }

    public async Task<MurabahaFacilityDto> DisburseMurabahaToVendorAsync(
        Guid facilityId,
        DisburseMurabahaToVendorRequestDto request,
        CancellationToken ct = default)
    {
        var facility = await _repository.GetMurabahaByIdAsync(facilityId, ct)
            ?? throw new KeyNotFoundException($"Murabaha facility '{facilityId}' not found.");

        var order = new VendorDisbursementOrder(
            request.VendorName,
            request.VendorAccountNumber,
            request.VendorBankRoutingNo,
            request.InvoiceNumber,
            request.InvoiceAmount,
            facility.InspectionProofReference ?? "PRE-VERIFIED-INSPECTION",
            DateTime.UtcNow);

        facility.DisburseToVendor(order, request.AuthorizerOfficerId);
        await _repository.SaveMurabahaFacilityAsync(facility, ct);

        return MapToDto(facility);
    }

    public async Task<MurabahaFacilityDto> GetMurabahaFacilityAsync(
        Guid facilityId,
        CancellationToken ct = default)
    {
        var facility = await _repository.GetMurabahaByIdAsync(facilityId, ct)
            ?? throw new KeyNotFoundException($"Murabaha facility '{facilityId}' not found.");

        return MapToDto(facility);
    }

    // 4. Quard Financing Facilities
    public async Task<QuardFacilityDto> CreateQuardFacilityAsync(
        CreateQuardFacilityRequestDto request,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetQuardByFacilityNumberAsync(request.FacilityNumber, ct);
        if (existing is not null)
        {
            throw new InvariantViolationException(
                "DuplicateFacilityInvariant",
                "BRPD 4.3.4",
                $"Quard facility number '{request.FacilityNumber}' already exists.");
        }

        var facility = new QuardFinancingFacility(
            request.FacilityNumber,
            request.Cif,
            request.PrincipalAmount,
            request.ActualAdministrativeFee,
            request.TenorMonths);

        await _repository.SaveQuardFacilityAsync(facility, ct);
        return MapToDto(facility);
    }

    public async Task<QuardFacilityDto> GetQuardFacilityAsync(
        Guid facilityId,
        CancellationToken ct = default)
    {
        var facility = await _repository.GetQuardByIdAsync(facilityId, ct)
            ?? throw new KeyNotFoundException($"Quard facility '{facilityId}' not found.");

        return MapToDto(facility);
    }

    // 5. Ta'widh Charity Fund
    public async Task<TaWidhRecordDto> AccrueTaWidhPenaltyAsync(
        AccrueTaWidhPenaltyRequestDto request,
        CancellationToken ct = default)
    {
        var record = new ShariahTaWidhCharityFund(
            request.FacilityReference,
            request.CustomerCif,
            request.OverduePenaltyAmount);

        await _repository.SaveTaWidhRecordAsync(record, ct);
        return MapToDto(record);
    }

    public async Task<TaWidhRecordDto> DisburseCharityFundAsync(
        Guid recordId,
        DisburseCharityFundRequestDto request,
        CancellationToken ct = default)
    {
        var record = await _repository.GetTaWidhByIdAsync(recordId, ct)
            ?? throw new KeyNotFoundException($"Ta'widh record '{recordId}' not found.");

        record.ApproveByShariahBoard(request.ApprovedSocialWelfareCause);
        record.DisburseToWelfare(request.DisbursedByOfficerId);

        await _repository.SaveTaWidhRecordAsync(record, ct);
        return MapToDto(record);
    }

    public async Task<TaWidhFundSummaryDto> GetTaWidhSummaryAsync(CancellationToken ct = default)
    {
        var records = await _repository.GetAllTaWidhRecordsAsync(ct);

        decimal totalSuspense = records
            .Where(r => r.Status != TaWidhStatus.DisbursedToCharity)
            .Sum(r => r.OverduePenaltyAmount);

        int accruedCount = records.Count(r => r.Status == TaWidhStatus.AccruedInSuspense);
        int approvedCount = records.Count(r => r.Status == TaWidhStatus.ApprovedByShariahBoard);
        int disbursedCount = records.Count(r => r.Status == TaWidhStatus.DisbursedToCharity);

        return new TaWidhFundSummaryDto(
            totalSuspense,
            accruedCount,
            approvedCount,
            disbursedCount,
            records.Select(MapToDto).ToList());
    }

    // Mapping Helpers
    private static MudarabaPoolDto MapToDto(MudarabaInvestmentPool p) => new(
        p.Id,
        p.PoolName,
        p.PoolType,
        p.Currency,
        p.TotalDepositBalance,
        p.MudaribShareRatio,
        p.RabUlMalShareRatio,
        p.PerRatio,
        p.IrrRatio,
        p.WeightagesJson,
        p.AccumulatedPerBalance,
        p.AccumulatedIrrBalance,
        p.CreatedAtUtc,
        p.LastDistributionDateUtc);

    private static AlWadiahAccountDto MapToDto(AlWadiahAccount a) => new(
        a.Id,
        a.AccountNumber,
        a.Cif,
        a.Currency,
        a.Balance,
        a.InterestRate,
        a.SafeCustodyContractRef,
        a.IsActive,
        a.CreatedAtUtc);

    private static MurabahaFacilityDto MapToDto(MurabahaFinancingFacility m) => new(
        m.Id,
        m.FacilityNumber,
        m.Cif,
        m.BranchCode,
        m.PurchaseCost,
        m.ProfitMarginAmount,
        m.TotalSalePrice,
        m.RepaymentTenorMonths,
        m.MonthlyInstallment,
        m.OutstandingBalance,
        m.Status,
        m.GoodsDescription,
        m.InspectionProofReference,
        m.VendorDisbursementOrderJson,
        m.DisbursedAtUtc,
        m.CreatedAtUtc);

    private static QuardFacilityDto MapToDto(QuardFinancingFacility q) => new(
        q.Id,
        q.FacilityNumber,
        q.Cif,
        q.PrincipalAmount,
        q.ActualAdministrativeFee,
        q.InterestRate,
        q.ProfitRate,
        q.OutstandingBalance,
        q.TenorMonths,
        q.IsSettled,
        q.CreatedAtUtc);

    private static TaWidhRecordDto MapToDto(ShariahTaWidhCharityFund t) => new(
        t.Id,
        t.FacilityReference,
        t.CustomerCif,
        t.OverduePenaltyAmount,
        t.AccruedAtUtc,
        t.Status,
        t.ApprovedSocialWelfareCause,
        t.DisbursedAtUtc,
        t.DisbursedByOfficerId);
}
