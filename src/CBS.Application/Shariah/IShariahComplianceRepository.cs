using CBS.Domain.Shariah;

namespace CBS.Application.Shariah;

public interface IShariahComplianceRepository
{
    // Mudaraba Investment Pools
    Task<MudarabaInvestmentPool?> GetPoolByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MudarabaInvestmentPool>> GetAllPoolsAsync(CancellationToken ct = default);
    Task SavePoolAsync(MudarabaInvestmentPool pool, CancellationToken ct = default);

    // Al-Wadiah Current Accounts
    Task<AlWadiahAccount?> GetAlWadiahByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<IReadOnlyList<AlWadiahAccount>> GetAlWadiahByCifAsync(string cif, CancellationToken ct = default);
    Task SaveAlWadiahAccountAsync(AlWadiahAccount account, CancellationToken ct = default);

    // Murabaha Facilities
    Task<MurabahaFinancingFacility?> GetMurabahaByIdAsync(Guid id, CancellationToken ct = default);
    Task<MurabahaFinancingFacility?> GetMurabahaByFacilityNumberAsync(string facilityNumber, CancellationToken ct = default);
    Task<IReadOnlyList<MurabahaFinancingFacility>> GetMurabahaByCifAsync(string cif, CancellationToken ct = default);
    Task SaveMurabahaFacilityAsync(MurabahaFinancingFacility facility, CancellationToken ct = default);

    // Quard Facilities
    Task<QuardFinancingFacility?> GetQuardByIdAsync(Guid id, CancellationToken ct = default);
    Task<QuardFinancingFacility?> GetQuardByFacilityNumberAsync(string facilityNumber, CancellationToken ct = default);
    Task<IReadOnlyList<QuardFinancingFacility>> GetQuardByCifAsync(string cif, CancellationToken ct = default);
    Task SaveQuardFacilityAsync(QuardFinancingFacility facility, CancellationToken ct = default);

    // Ta'widh Charity Fund
    Task<ShariahTaWidhCharityFund?> GetTaWidhByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ShariahTaWidhCharityFund>> GetAllTaWidhRecordsAsync(CancellationToken ct = default);
    Task SaveTaWidhRecordAsync(ShariahTaWidhCharityFund record, CancellationToken ct = default);

    // Seed Data
    Task SeedDefaultIslamicPoolsIfEmptyAsync(CancellationToken ct = default);
}
