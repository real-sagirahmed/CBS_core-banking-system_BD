using CBS.Application.Common.Interfaces;
using CBS.Application.Deposits.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Deposits;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Deposits.Commands;

public class DpsCommandHandler
{
    private readonly IDpsRepository _dpsRepository;
    private readonly IDepositLienRepository _depositLienRepository;
    private readonly ITermDepositRepository _termDepositRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DpsCommandHandler(
        IDpsRepository dpsRepository,
        IDepositLienRepository depositLienRepository,
        ITermDepositRepository termDepositRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _dpsRepository = dpsRepository ?? throw new ArgumentNullException(nameof(dpsRepository));
        _depositLienRepository = depositLienRepository ?? throw new ArgumentNullException(nameof(depositLienRepository));
        _termDepositRepository = termDepositRepository ?? throw new ArgumentNullException(nameof(termDepositRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DpsAccountResponseDto> HandleOpenDpsAccountAsync(OpenDpsRequest request, CancellationToken ct = default)
    {
        var dpsAcc = AccountNumber.From(request.DpsAccountNumber);
        var linkedCasaAcc = AccountNumber.From(request.LinkedCasaAccountNumber);
        var cif = new CifNumber(request.CustomerCif);
        var currency = Currency.FromCode(request.Currency);
        var installmentMoney = new Money(request.MonthlyInstallment, currency);

        // Verify linked CASA account exists
        var linkedCasa = await _accountRepository.GetByAccountNumberAsync(linkedCasaAcc, ct);
        if (linkedCasa is null)
        {
            throw new InvariantViolationException(
                "LinkedCasaNotFoundInvariant",
                "BRPD Circular 12/2024 (3.8.9)",
                $"Linked CASA account '{request.LinkedCasaAccountNumber}' was not found.");
        }

        string dpsId = $"DPS-{request.DpsAccountNumber}";
        var dps = new DpsAccount(
            dpsId,
            dpsAcc,
            linkedCasaAcc,
            cif,
            installmentMoney,
            request.TenorYears,
            request.ContractedRate,
            request.PrematurePenaltyRate,
            request.DebitDayOfMonth,
            request.StartDate,
            request.SettlementMode);

        await _dpsRepository.AddAsync(dps, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(dps);
    }

    public async Task<DpsAccountResponseDto?> HandleGetDpsAccountAsync(string dpsAccountId, CancellationToken ct = default)
    {
        var dps = await _dpsRepository.GetByIdAsync(dpsAccountId, ct);
        return dps is null ? null : MapToDto(dps);
    }

    public async Task<DpsAccountResponseDto> HandleCollectInstallmentAsync(
        string dpsAccountId,
        CollectInstallmentRequest request,
        CancellationToken ct = default)
    {
        var dps = await _dpsRepository.GetByIdAsync(dpsAccountId, ct);
        if (dps is null)
        {
            throw new InvariantViolationException(
                "DpsAccountNotFound",
                "BRPD Circular 12/2024 (3.8.1)",
                $"DPS account '{dpsAccountId}' was not found.");
        }

        var currency = dps.MonthlyInstallmentAmount.Currency;
        var paidMoney = new Money(request.PaidAmount, currency);
        var lateFeeMoney = request.LateFee.HasValue ? new Money(request.LateFee.Value, currency) : null;

        dps.ProcessInstallmentPayment(
            request.InstallmentNumber,
            paidMoney,
            request.PaymentDate,
            lateFeeMoney,
            request.VoucherNumber);

        await _dpsRepository.UpdateAsync(dps, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(dps);
    }

    public async Task<PrematureDpsQuoteResponse> HandleGetPrematureQuoteAsync(
        string dpsAccountId,
        DateOnly encashmentDate,
        bool hasTaxIdNumber,
        CancellationToken ct = default)
    {
        var dps = await _dpsRepository.GetByIdAsync(dpsAccountId, ct);
        if (dps is null)
        {
            throw new InvariantViolationException(
                "DpsAccountNotFound",
                "BRPD Circular 12/2024 (3.8.1)",
                $"DPS account '{dpsAccountId}' was not found.");
        }

        var quote = dps.CalculatePrematureEncashment(encashmentDate, hasTaxIdNumber);

        return new PrematureDpsQuoteResponse(
            DpsAccountId: dps.Id,
            TotalPrincipal: quote.TotalPrincipal.Amount,
            GrossInterest: quote.GrossInterest.Amount,
            WithholdingTax: quote.WithholdingTax.Amount,
            NetPayableAmount: quote.NetPayableAmount.Amount,
            EffectiveRate: quote.EffectiveRateApplied,
            MonthsCompleted: quote.MonthsCompleted,
            Currency: quote.TotalPrincipal.Currency.Code);
    }

    public async Task<DpsAccountResponseDto> HandlePrematureEncashAsync(
        string dpsAccountId,
        PrematureEncashDpsRequest request,
        CancellationToken ct = default)
    {
        var dps = await _dpsRepository.GetByIdAsync(dpsAccountId, ct);
        if (dps is null)
        {
            throw new InvariantViolationException(
                "DpsAccountNotFound",
                "BRPD Circular 12/2024 (3.8.1)",
                $"DPS account '{dpsAccountId}' was not found.");
        }

        // Calculate premature encashment quote (throws if active lien is marked)
        var quote = dps.CalculatePrematureEncashment(request.EncashmentDate, request.HasTaxIdNumber);
        dps.MarkEncashed(request.EncashmentDate);

        // Credit proceeds to linked CASA account
        var linkedCasa = await _accountRepository.GetByAccountNumberAsync(dps.LinkedCasaAccountNumber, ct);
        if (linkedCasa != null)
        {
            linkedCasa.Credit(quote.NetPayableAmount, $"DPS premature encashment proceeds {dps.DpsAccountNumber}");
            await _accountRepository.UpdateAsync(linkedCasa, ct);
        }

        await _dpsRepository.UpdateAsync(dps, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(dps);
    }

    public async Task<int> HandleExecuteDueDpsSweepsAsync(DateOnly evaluationDate, CancellationToken ct = default)
    {
        var dueAccounts = await _dpsRepository.GetActiveAccountsDueForSweepAsync(evaluationDate, ct);
        int processedCount = 0;

        foreach (var dps in dueAccounts)
        {
            // Find current pending installment
            var pendingSchedule = dps.Installments
                .OrderBy(s => s.InstallmentNumber)
                .FirstOrDefault(s => s.Status == Domain.Enums.DpsInstallmentStatus.Pending && s.DueDate <= evaluationDate);

            if (pendingSchedule is null) continue;

            var linkedCasa = await _accountRepository.GetByAccountNumberAsync(dps.LinkedCasaAccountNumber, ct);
            if (linkedCasa is null) continue;

            // Attempt auto-debit sweep
            if (linkedCasa.Balance >= pendingSchedule.DueAmount)
            {
                linkedCasa.Debit(pendingSchedule.DueAmount, $"DPS monthly installment sweep {dps.DpsAccountNumber}");
                string voucher = $"SWP-DPS-{dps.DpsAccountNumber}-{pendingSchedule.InstallmentNumber}";
                dps.ProcessInstallmentPayment(pendingSchedule.InstallmentNumber, pendingSchedule.DueAmount, evaluationDate, null, voucher);
                await _accountRepository.UpdateAsync(linkedCasa, ct);
                processedCount++;
            }
            else
            {
                // Insufficient balance: record missed installment / late fine
                dps.RecordMissedInstallment(evaluationDate);
            }

            await _dpsRepository.UpdateAsync(dps, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return processedCount;
    }

    public async Task<DepositLienResponseDto> HandlePlaceDepositLienAsync(PlaceDepositLienRequest request, CancellationToken ct = default)
    {
        string lienId = $"LIEN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var currency = Currency.FromCode(request.Currency);
        var sanctionedLimit = new Money(request.SanctionedLoanLimit, currency);
        var encumberedAmount = new Money(request.EncumberedAmount, currency);

        if (string.Equals(request.DepositType, "DPS", StringComparison.OrdinalIgnoreCase))
        {
            var dps = await _dpsRepository.GetByIdAsync(request.DepositAccountId, ct);
            if (dps is null)
            {
                throw new InvariantViolationException(
                    "DepositAccountNotFound",
                    "BRPD Circular 12/2024 (3.8.10)",
                    $"DPS account '{request.DepositAccountId}' was not found.");
            }

            dps.MarkLien(lienId, $"Lien for loan facility '{request.LoanAccountId}'");
            await _dpsRepository.UpdateAsync(dps, ct);
        }
        else if (string.Equals(request.DepositType, "FDR", StringComparison.OrdinalIgnoreCase))
        {
            var fdr = await _termDepositRepository.GetContractByIdAsync(request.DepositAccountId, ct);
            if (fdr is null)
            {
                throw new InvariantViolationException(
                    "DepositAccountNotFound",
                    "BRPD Circular 12/2024 (3.6.1)",
                    $"FDR contract '{request.DepositAccountId}' was not found.");
            }

            fdr.MarkLien(sanctionedLimit, $"Lien for loan facility '{request.LoanAccountId}'");
            await _termDepositRepository.UpdateAsync(fdr, ct);
        }
        else
        {
            throw new InvariantViolationException(
                "InvalidDepositTypeInvariant",
                "BRPD Circular 12/2024 (3.8.10)",
                $"Deposit type '{request.DepositType}' is not supported for lien marking. Use 'DPS' or 'FDR'.");
        }

        var lien = new DepositLienFacility(
            lienId,
            request.DepositAccountId,
            request.DepositType.ToUpperInvariant(),
            request.LoanAccountId,
            sanctionedLimit,
            encumberedAmount,
            request.MaximumLtvPercentage,
            request.OfficerId,
            request.ReferenceDocument);

        await _depositLienRepository.AddAsync(lien, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapLienToDto(lien);
    }

    public async Task<DepositLienResponseDto> HandleReleaseDepositLienAsync(string lienId, ReleaseDepositLienRequest request, CancellationToken ct = default)
    {
        var lien = await _depositLienRepository.GetByIdAsync(lienId, ct);
        if (lien is null)
        {
            throw new InvariantViolationException(
                "LienNotFound",
                "BRPD Circular 12/2024 (3.8.10)",
                $"Deposit lien '{lienId}' was not found.");
        }

        lien.Release(request.OfficerId, request.Reason);

        if (string.Equals(lien.DepositType, "DPS", StringComparison.OrdinalIgnoreCase))
        {
            var dps = await _dpsRepository.GetByIdAsync(lien.DepositAccountId, ct);
            if (dps != null && dps.IsLienMarked)
            {
                dps.ReleaseLien(lienId);
                await _dpsRepository.UpdateAsync(dps, ct);
            }
        }
        else if (string.Equals(lien.DepositType, "FDR", StringComparison.OrdinalIgnoreCase))
        {
            var fdr = await _termDepositRepository.GetContractByIdAsync(lien.DepositAccountId, ct);
            if (fdr != null && fdr.IsLienMarked)
            {
                fdr.ReleaseLien(lienId);
                await _termDepositRepository.UpdateAsync(fdr, ct);
            }
        }

        await _depositLienRepository.UpdateAsync(lien, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapLienToDto(lien);
    }

    public async Task<DepositLienResponseDto?> HandleGetActiveLienAsync(string depositAccountId, CancellationToken ct = default)
    {
        var lien = await _depositLienRepository.GetActiveLienByDepositAccountAsync(depositAccountId, ct);
        return lien is null ? null : MapLienToDto(lien);
    }

    private static DpsAccountResponseDto MapToDto(DpsAccount dps)
    {
        var installments = dps.Installments
            .OrderBy(i => i.InstallmentNumber)
            .Select(i => new DpsInstallmentDto(
                i.InstallmentNumber,
                i.DueDate,
                i.PaidDate,
                i.DueAmount.Amount,
                i.LateFee.Amount,
                i.Status.ToString(),
                i.PaymentVoucherNumber))
            .ToList();

        return new DpsAccountResponseDto(
            dps.Id,
            dps.DpsAccountNumber.Value,
            dps.LinkedCasaAccountNumber.Value,
            dps.CustomerCif.Value,
            dps.MonthlyInstallmentAmount.Amount,
            dps.MonthlyInstallmentAmount.Currency.Code,
            dps.TenorYears,
            dps.ContractedAnnualRate,
            dps.PrematureSavingsPenaltyRate,
            dps.DebitDayOfMonth,
            dps.StartDate,
            dps.MaturityDate,
            dps.TotalPrincipalPaid.Amount,
            dps.TotalInterestAccrued.Amount,
            dps.PaidInstallmentsCount,
            dps.TotalInstallments,
            dps.ConsecutiveDefaultCount,
            dps.Status.ToString(),
            dps.IsLienMarked,
            dps.ActiveLienFacilityId,
            installments);
    }

    private static DepositLienResponseDto MapLienToDto(DepositLienFacility lien)
    {
        return new DepositLienResponseDto(
            lien.Id,
            lien.DepositAccountId,
            lien.DepositType,
            lien.LoanAccountId,
            lien.SanctionedLoanLimit.Amount,
            lien.EncumberedAmount.Amount,
            lien.SanctionedLoanLimit.Currency.Code,
            lien.MaximumLtvPercentage,
            lien.Status.ToString(),
            lien.PlacedAtUtc,
            lien.ReleasedAtUtc,
            lien.PlacedByOfficerId,
            lien.ReleasedByOfficerId,
            lien.ReferenceDocument);
    }
}
