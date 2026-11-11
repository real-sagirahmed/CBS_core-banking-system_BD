using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Domain.BranchCash;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.BranchCash.Commands;

// ARCHITECTURAL INTENT: Branch Vault Dual-Custody Command Handler (BRPD Circular 12/2024 Module 1.7.1-iii, 1.7.2, 1.7.12).
// Orchestrates physical branch vault balances, dual key approvals, currency chest inward/outward postings,
// teller replenishments, and inter-teller denomination swaps.
public class BranchVaultCommandHandler
{
    private readonly IBranchVaultRepository _vaultRepository;
    private readonly ITellerDrawerRepository _drawerRepository;
    private readonly IGeneralLedgerService _glService;
    private readonly IUnitOfWork _unitOfWork;

    public BranchVaultCommandHandler(
        IBranchVaultRepository vaultRepository,
        ITellerDrawerRepository drawerRepository,
        IGeneralLedgerService glService,
        IUnitOfWork unitOfWork)
    {
        _vaultRepository = vaultRepository ?? throw new ArgumentNullException(nameof(vaultRepository));
        _drawerRepository = drawerRepository ?? throw new ArgumentNullException(nameof(drawerRepository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BranchVaultResponseDto> InitializeVaultAsync(InitializeBranchVaultRequest req, CancellationToken ct = default)
    {
        var existing = await _vaultRepository.GetByBranchCodeAsync(req.BranchCode, ct);
        if (existing is not null)
        {
            throw new InvalidOperationException($"Branch vault for branch '{req.BranchCode}' is already initialized.");
        }

        var bundle = new CashDenominationBundle(req.Denominations);
        var initialAmount = Money.Bdt(req.InitialBalance);
        var insuredLimit = Money.Bdt(req.InsuredCashLimit);

        var vault = new BranchVault(
            id: Guid.NewGuid(),
            branchCode: req.BranchCode,
            vaultCustodianUserId: req.VaultCustodianUserId,
            jointCustodianUserId: req.JointCustodianUserId,
            initialBalance: initialAmount,
            insuredCashLimit: insuredLimit,
            denominations: bundle);

        await _vaultRepository.AddAsync(vault, ct);

        if (req.InitialBalance > 0m)
        {
            await _glService.PostVaultMovementAsync(
                req.BranchCode,
                req.InitialBalance,
                isVaultToTeller: false,
                req.VaultCustodianUserId,
                req.JointCustodianUserId,
                ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(vault);
    }

    public async Task<BranchVaultResponseDto> GetVaultByBranchAsync(string branchCode, CancellationToken ct = default)
    {
        var vault = await _vaultRepository.GetByBranchCodeAsync(branchCode, ct)
            ?? throw new KeyNotFoundException($"Branch vault for branch '{branchCode}' not found.");

        return MapToDto(vault);
    }

    public async Task<BranchVaultResponseDto> ProcessChestMovementAsync(ChestCashMovementRequest req, CancellationToken ct = default)
    {
        var vault = await _vaultRepository.GetByBranchCodeAsync(req.BranchCode, ct)
            ?? throw new KeyNotFoundException($"Branch vault for branch '{req.BranchCode}' not found.");

        var amount = Money.Bdt(req.Amount);
        var bundle = new CashDenominationBundle(req.Denominations);

        if (req.MovementType == CashMovementType.ChestToVault)
        {
            vault.DepositCashFromExternalChest(amount, bundle, req.ChestReference, req.CustodianUserId, req.JointCustodianUserId);
            await _glService.PostVaultMovementAsync(
                req.BranchCode,
                req.Amount,
                isVaultToTeller: false,
                req.CustodianUserId,
                req.JointCustodianUserId,
                ct);
        }
        else if (req.MovementType == CashMovementType.VaultToChest)
        {
            vault.RemitCashToExternalChest(amount, bundle, req.ChestReference, req.CustodianUserId, req.JointCustodianUserId);
            await _glService.PostVaultMovementAsync(
                req.BranchCode,
                req.Amount,
                isVaultToTeller: true,
                req.CustodianUserId,
                req.JointCustodianUserId,
                ct);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported chest movement type: {req.MovementType}");
        }

        await _vaultRepository.UpdateAsync(vault, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(vault);
    }

    public async Task<BranchVaultResponseDto> ReplenishTellerAsync(VaultTellerTransferRequest req, CancellationToken ct = default)
    {
        var vault = await _vaultRepository.GetByBranchCodeAsync(req.BranchCode, ct)
            ?? throw new KeyNotFoundException($"Branch vault for branch '{req.BranchCode}' not found.");
        var drawer = await _drawerRepository.GetByIdAsync(req.DrawerId, ct)
            ?? throw new KeyNotFoundException($"Teller drawer '{req.DrawerId}' not found.");

        var amount = Money.Bdt(req.Amount);
        var bundle = new CashDenominationBundle(req.Denominations);

        vault.AllocateCashToTeller(amount, bundle, req.CashierUserId, req.CustodianUserId);
        drawer.ProcessCashDeposit(amount, bundle, supervisorApprovalId: req.CustodianUserId);

        await _glService.PostVaultMovementAsync(
            req.BranchCode,
            req.Amount,
            isVaultToTeller: true,
            req.CashierUserId,
            req.CustodianUserId,
            ct);

        await _vaultRepository.UpdateAsync(vault, ct);
        await _drawerRepository.UpdateAsync(drawer, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(vault);
    }

    public async Task<BranchVaultResponseDto> ReceiveFromTellerAsync(VaultTellerTransferRequest req, CancellationToken ct = default)
    {
        var vault = await _vaultRepository.GetByBranchCodeAsync(req.BranchCode, ct)
            ?? throw new KeyNotFoundException($"Branch vault for branch '{req.BranchCode}' not found.");
        var drawer = await _drawerRepository.GetByIdAsync(req.DrawerId, ct)
            ?? throw new KeyNotFoundException($"Teller drawer '{req.DrawerId}' not found.");

        var amount = Money.Bdt(req.Amount);
        var bundle = new CashDenominationBundle(req.Denominations);

        drawer.TransferToVault(amount, bundle, req.CustodianUserId);
        vault.ReceiveCashFromTeller(amount, bundle, req.CashierUserId, req.CustodianUserId);

        await _glService.PostVaultMovementAsync(
            req.BranchCode,
            req.Amount,
            isVaultToTeller: false,
            req.CashierUserId,
            req.CustodianUserId,
            ct);

        await _drawerRepository.UpdateAsync(drawer, ct);
        await _vaultRepository.UpdateAsync(vault, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(vault);
    }

    public async Task<InterTellerSwapResponseDto> SwapTellerDenominationsAsync(InterTellerDenominationSwapRequest req, CancellationToken ct = default)
    {
        if (req.SourceDrawerId == req.TargetDrawerId)
        {
            throw new InvariantViolationException(
                "SelfSwapProhibitedInvariant",
                "BRPD Circular 12/2024 (1.7.2-ii)",
                "Source drawer and target drawer cannot be the same drawer.");
        }

        var sourceDrawer = await _drawerRepository.GetByIdAsync(req.SourceDrawerId, ct)
            ?? throw new KeyNotFoundException($"Source teller drawer '{req.SourceDrawerId}' not found.");
        var targetDrawer = await _drawerRepository.GetByIdAsync(req.TargetDrawerId, ct)
            ?? throw new KeyNotFoundException($"Target teller drawer '{req.TargetDrawerId}' not found.");

        var givenBundle = new CashDenominationBundle(req.GivenDenominations);
        var receivedBundle = new CashDenominationBundle(req.ReceivedDenominations);

        if (givenBundle.TotalAmount != receivedBundle.TotalAmount)
        {
            throw new InvariantViolationException(
                "DenominationSwapValueMismatchInvariant",
                "BRPD Circular 12/2024 (1.7.2-ii)",
                $"Given bundle sum ({givenBundle.TotalAmount}) must exactly equal received bundle sum ({receivedBundle.TotalAmount}).");
        }

        // Source gives givenBundle, receives receivedBundle
        sourceDrawer.SwapDenominations(givenBundle, receivedBundle, req.SupervisorApprovalId);

        // Target gives receivedBundle, receives givenBundle
        targetDrawer.SwapDenominations(receivedBundle, givenBundle, req.SupervisorApprovalId);

        await _drawerRepository.UpdateAsync(sourceDrawer, ct);
        await _drawerRepository.UpdateAsync(targetDrawer, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new InterTellerSwapResponseDto(
            req.SourceDrawerId,
            req.TargetDrawerId,
            givenBundle.TotalAmount.Amount,
            $"Inter-teller denomination swap of BDT {givenBundle.TotalAmount.Amount:N2} executed successfully under dual control.");
    }

    public async Task<BranchVaultResponseDto> ToggleEveningBankingAsync(ToggleEveningBankingRequest req, CancellationToken ct = default)
    {
        var vault = await _vaultRepository.GetByBranchCodeAsync(req.BranchCode, ct)
            ?? throw new KeyNotFoundException($"Branch vault for branch '{req.BranchCode}' not found.");

        if (req.OpenSession)
        {
            vault.OpenEveningBankingSession(req.AuthorizedOfficerUserId);
        }
        else
        {
            vault.CloseEveningBankingSession(req.AuthorizedOfficerUserId);
        }

        await _vaultRepository.UpdateAsync(vault, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(vault);
    }

    private static BranchVaultResponseDto MapToDto(BranchVault vault) => new(
        vault.Id,
        vault.BranchCode,
        vault.VaultCustodianUserId,
        vault.JointCustodianUserId,
        vault.CurrentBalance.Amount,
        vault.InsuredCashLimit.Amount,
        vault.IsInsuredLimitExceeded(),
        vault.GetExcessOverInsuredLimit(),
        vault.Denominations.Counts.ToDictionary(kv => kv.Key, kv => kv.Value),
        vault.IsVaultLocked,
        vault.EveningBankingSessionActive);
}
