using System.Collections.Concurrent;
using CBS.Application.Common.Interfaces;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TradeFinance.Services;

// ARCHITECTURAL INTENT: Master Export LC and Back-to-Back (B2B) Import LC Management Service
// enforcing Statutory Value Addition Ratio (maximum 75% B2B ceiling / minimum 25% value addition),
// B2B expiry/shipment date hierarchy, dual-control maker-checker invariants, and closure protections
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 5.2.2-i, 5.5.1, and 5.6.1.
public sealed class MasterExportLcService : IMasterExportLcService
{
    private readonly IMasterExportLcRepository _masterRepo;
    private readonly IUnitOfWork _unitOfWork;
    private static readonly ConcurrentDictionary<string, List<B2bLcIssuedResponseDto>> MasterB2bRegistry = new();

    public MasterExportLcService(
        IMasterExportLcRepository masterRepo,
        IUnitOfWork unitOfWork)
    {
        _masterRepo = masterRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<MasterExportLcResponseDto> RegisterMasterExportLcAsync(
        RegisterMasterExportLcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var existing = await _masterRepo.GetByNumberAsync(request.MasterLcNumber, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException($"Master Export LC / Sales Contract '{request.MasterLcNumber}' already exists.");

        var currency = Currency.FromCode(request.Currency);
        var totalMoney = new Money(request.TotalAmount, currency);
        var cif = new CifNumber(request.ExporterCif);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var issueDate = request.IssueDate == default ? today : request.IssueDate;
        var shipmentDate = request.LatestShipmentDate == default ? today.AddMonths(4) : request.LatestShipmentDate;
        var expiryDate = request.ExpiryDate == default ? shipmentDate.AddDays(21) : request.ExpiryDate;

        var masterLc = new MasterExportLc(
            masterLcNumber: request.MasterLcNumber,
            contractType: request.ContractType,
            exporterCif: cif,
            issuingBankBic: request.IssuingBankBic,
            issuingBankName: request.IssuingBankName,
            buyerName: request.BuyerName,
            buyerCountry: request.BuyerCountry,
            totalAmount: totalMoney,
            valueAdditionPercentage: request.ValueAdditionPercentage,
            issueDate: issueDate,
            expiryDate: expiryDate,
            latestShipmentDate: shipmentDate,
            branchCode: request.BranchCode,
            makerUserId: request.MakerUserId,
            replacedContractRef: request.ReplacedContractRef);

        await _masterRepo.AddAsync(masterLc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(masterLc);
    }

    public async Task<MasterExportLcResponseDto> AuthorizeMasterExportLcAsync(
        string masterLcNumber,
        AuthorizeMasterExportLcRequest request,
        CancellationToken cancellationToken = default)
    {
        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        masterLc.Authorize(request.CheckerUserId);

        await _masterRepo.UpdateAsync(masterLc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(masterLc);
    }

    public async Task<MasterExportLcResponseDto> GetMasterExportLcAsync(
        string masterLcNumber,
        CancellationToken cancellationToken = default)
    {
        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        return MapToDto(masterLc);
    }

    public async Task<B2bLcIssuedResponseDto> IssueB2bLcUnderMasterAsync(
        string masterLcNumber,
        OpenB2bLcUnderMasterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        var currency = Currency.FromCode(request.Currency);
        var b2bMoney = new Money(request.Amount, currency);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shipmentDate = request.ShipmentDate == default ? today.AddMonths(2) : request.ShipmentDate;
        var expiryDate = request.ExpiryDate == default ? shipmentDate.AddDays(15) : request.ExpiryDate;

        // INVARIANTS: Value addition ceiling, date hierarchy, status (BRPD 5.6.1-ii, v, vi, vii)
        masterLc.AllocateB2bLimit(b2bMoney, shipmentDate, expiryDate, today);

        await _masterRepo.UpdateAsync(masterLc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var issuedB2b = new B2bLcIssuedResponseDto(
            B2bLcNumber: request.B2bLcNumber,
            MasterLcNumber: masterLc.Id,
            Amount: request.Amount,
            Currency: request.Currency,
            CashMarginAmount: request.CashMarginAmount,
            ShipmentDate: shipmentDate,
            ExpiryDate: expiryDate,
            ProformaInvoiceNumber: request.ProformaInvoiceNumber,
            LcafNumber: request.LcafNumber,
            ImpFormNumber: request.ImpFormNumber,
            RemainingMasterCapacity: masterLc.RemainingB2bCapacity.Amount,
            IssuedAtUtc: DateTime.UtcNow);

        MasterB2bRegistry.AddOrUpdate(
            masterLc.Id,
            new List<B2bLcIssuedResponseDto> { issuedB2b },
            (_, list) =>
            {
                lock (list) { list.Add(issuedB2b); }
                return list;
            });

        return issuedB2b;
    }

    public async Task<MasterExportLcResponseDto> AmendMasterExportLcAsync(
        string masterLcNumber,
        AmendMasterLcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        var newAmount = new Money(request.NewTotalAmount, masterLc.TotalAmount.Currency);
        masterLc.AmendAmount(newAmount, request.MakerUserId, request.CheckerUserId);

        await _masterRepo.UpdateAsync(masterLc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(masterLc);
    }

    public async Task<MasterExportLcResponseDto> CloseMasterExportLcAsync(
        string masterLcNumber,
        CloseMasterLcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        masterLc.Close(request.MakerUserId, request.CheckerUserId);

        await _masterRepo.UpdateAsync(masterLc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(masterLc);
    }

    public async Task<MasterLcHistoryReportDto> GetMasterLcHistoryReportAsync(
        string masterLcNumber,
        CancellationToken cancellationToken = default)
    {
        var masterLc = await _masterRepo.GetByNumberAsync(masterLcNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Master Export LC '{masterLcNumber}' was not found.");

        MasterB2bRegistry.TryGetValue(masterLc.Id, out var b2bList);
        var activeB2bs = b2bList ?? new List<B2bLcIssuedResponseDto>();

        return new MasterLcHistoryReportDto(
            MasterLcNumber: masterLc.Id,
            ExporterCif: masterLc.ExporterCif.Value,
            Status: masterLc.Status.ToString(),
            MasterAmount: masterLc.TotalAmount.Amount,
            Currency: masterLc.TotalAmount.Currency.Code,
            StatutoryCeilingAmount: masterLc.MaxPermissibleB2bAmount.Amount,
            CumulativeB2bAllocatedAmount: masterLc.CumulativeB2bAllocatedAmount.Amount,
            RemainingCapacity: masterLc.RemainingB2bCapacity.Amount,
            TotalB2bCount: activeB2bs.Count,
            ActiveB2bLcs: activeB2bs,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private static MasterExportLcResponseDto MapToDto(MasterExportLc lc)
    {
        return new MasterExportLcResponseDto(
            MasterLcNumber: lc.Id,
            ContractType: lc.ContractType.ToString(),
            ExporterCif: lc.ExporterCif.Value,
            IssuingBankBic: lc.IssuingBankBic,
            IssuingBankName: lc.IssuingBankName,
            BuyerName: lc.BuyerName,
            BuyerCountry: lc.BuyerCountry,
            TotalAmount: lc.TotalAmount.Amount,
            Currency: lc.TotalAmount.Currency.Code,
            ValueAdditionPercentage: lc.ValueAdditionPercentage,
            MaxPermissibleB2bAmount: lc.MaxPermissibleB2bAmount.Amount,
            CumulativeB2bAllocatedAmount: lc.CumulativeB2bAllocatedAmount.Amount,
            RemainingB2bCapacity: lc.RemainingB2bCapacity.Amount,
            IssueDate: lc.IssueDate,
            ExpiryDate: lc.ExpiryDate,
            LatestShipmentDate: lc.LatestShipmentDate,
            Status: lc.Status.ToString(),
            BranchCode: lc.BranchCode,
            MakerUserId: lc.MakerUserId,
            CheckerUserId: lc.CheckerUserId,
            AuthorizedAtUtc: lc.AuthorizedAtUtc,
            ReplacedContractRef: lc.ReplacedContractRef);
    }
}
