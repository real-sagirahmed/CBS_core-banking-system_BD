using CBS.Application.Common.Interfaces;
using CBS.Application.Corporate.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Corporate;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Corporate.Commands;

// ARCHITECTURAL INTENT: High-Throughput Corporate Payroll and Dividend Batch Processing Engine
// strictly enforcing Bangladesh Bank BRPD Circular No. 12/2024 Module 1.6.16.
public class BulkCorporateCommandHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IBulkCorporateBatchRepository _batchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkCorporateCommandHandler(
        IAccountRepository accountRepository,
        IBulkCorporateBatchRepository batchRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _batchRepository = batchRepository ?? throw new ArgumentNullException(nameof(batchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BulkBatchSummaryDto> HandleUploadBatchAsync(
        UploadBulkBatchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var corpAccNo = AccountNumber.From(request.CorporateDebitAccountNumber.Trim());

        var corpAccount = await _accountRepository.GetByAccountNumberAsync(corpAccNo, ct)
            ?? throw new KeyNotFoundException($"Corporate debit account '{request.CorporateDebitAccountNumber}' not found.");

        if (!Enum.TryParse<BulkBatchType>(request.BatchType, true, out var batchType))
        {
            throw new ArgumentException($"Invalid bulk batch type: '{request.BatchType}'");
        }

        string batchId = $"BATCH-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var batch = new BulkCorporateBatch(
            batchId,
            batchType,
            corpAccNo,
            request.Narration,
            request.ValueDate,
            corpAccount.Balance.Currency,
            request.MakerUserId,
            DateTime.UtcNow);

        foreach (var item in request.Items)
        {
            batch.AddItem(
                item.BeneficiaryAccountNumber,
                item.BeneficiaryName,
                Money.Bdt(item.Amount),
                item.ReferenceNo);
        }

        // Pre-validation of individual items (BRPD 1.6.16-i)
        foreach (var item in batch.Items)
        {
            AccountNumber parsedAcc;
            try
            {
                parsedAcc = AccountNumber.From(item.BeneficiaryAccountNumber);
            }
            catch (Exception ex)
            {
                batch.MarkItemValidation(item.ItemIndex, false, $"Invalid account number format: {ex.Message}");
                continue;
            }

            var targetAcc = await _accountRepository.GetByAccountNumberAsync(parsedAcc, ct);
            if (targetAcc == null)
            {
                batch.MarkItemValidation(item.ItemIndex, false, "Beneficiary account does not exist in CBS.");
            }
            else if (targetAcc.Status is AccountOperatingStatus.Frozen or AccountOperatingStatus.Closed or AccountOperatingStatus.BlockedCredit or AccountOperatingStatus.Unclaimed10YearTransferred)
            {
                batch.MarkItemValidation(item.ItemIndex, false, $"Account is in '{targetAcc.Status}' status. Credit prohibited.");
            }
            else
            {
                batch.MarkItemValidation(item.ItemIndex, true, null);
            }
        }

        await _batchRepository.AddAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToSummaryDto(batch);
    }

    public async Task<BulkBatchSummaryDto> HandleAuthorizeBatchAsync(
        string batchId,
        AuthorizeBulkBatchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = await _batchRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Bulk batch '{batchId}' not found.");

        batch.Authorize(request.CheckerUserId, DateTime.UtcNow);

        await _batchRepository.UpdateAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToSummaryDto(batch);
    }

    public async Task<ExecuteBulkBatchResponseDto> HandleExecuteBatchAsync(
        string batchId,
        ExecuteBulkBatchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var batch = await _batchRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Bulk batch '{batchId}' not found.");

        if (batch.Status == BulkBatchStatus.Executed)
        {
            throw new InvalidOperationException($"Bulk batch '{batchId}' has already been executed.");
        }

        if (batch.AuthorizedByUserId == null)
        {
            batch.Authorize(request.CheckerUserId, DateTime.UtcNow);
        }

        var corpAccount = await _accountRepository.GetByAccountNumberAsync(batch.CorporateDebitAccountNumber, ct)
            ?? throw new KeyNotFoundException($"Corporate account '{batch.CorporateDebitAccountNumber.Value}' not found.");

        var validItems = batch.Items.Where(i => i.Status == BulkItemStatus.Valid).ToList();
        decimal totalValidAmount = validItems.Sum(i => i.Amount.Amount);
        var totalDebitMoney = Money.Bdt(totalValidAmount);

        // HARD LOCK: Available balance invariant on corporate account (BRPD 1.3.5, 1.6.6)
        if (totalDebitMoney > corpAccount.AvailableBalance)
        {
            throw new InvariantViolationException(
                "InsufficientCorporateFundsInvariant",
                "BRPD Circular 12/2024 (1.3.5, 1.6.6)",
                $"Corporate account '{corpAccount.Id.Value}' has available balance {corpAccount.AvailableBalance}, insufficient for batch debit total of {totalDebitMoney}.");
        }

        // Debit corporate master account
        corpAccount.Debit(totalDebitMoney, $"{batch.Narration} (Batch {batch.Id})", isCustomerInduced: true);
        await _accountRepository.UpdateAsync(corpAccount, ct);

        // Credit valid beneficiary accounts
        foreach (var item in validItems)
        {
            var targetAccNo = AccountNumber.From(item.BeneficiaryAccountNumber);
            var targetAccount = await _accountRepository.GetByAccountNumberAsync(targetAccNo, ct);

            if (targetAccount == null)
            {
                batch.RecordItemPosting(item.ItemIndex, false, null, "Account not found during posting.");
                continue;
            }

            try
            {
                targetAccount.Credit(item.Amount, $"{batch.Narration} - Ref {item.ReferenceNo}", isCustomerInduced: false);
                await _accountRepository.UpdateAsync(targetAccount, ct);
                string journalId = $"JRN-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
                batch.RecordItemPosting(item.ItemIndex, true, journalId, null);
            }
            catch (Exception ex)
            {
                batch.RecordItemPosting(item.ItemIndex, false, null, ex.Message);
            }
        }

        batch.CompleteExecution(DateTime.UtcNow);

        await _batchRepository.UpdateAsync(batch, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ExecuteBulkBatchResponseDto(
            batch.Id,
            batch.Status.ToString(),
            batch.TotalRecords,
            batch.SuccessCount,
            batch.FailedCount,
            batch.SuccessfulAmount.Amount,
            batch.FailedAmount.Amount,
            totalDebitMoney.Amount,
            batch.ExecutedAtUtc ?? DateTime.UtcNow,
            batch.Items.Select(MapToItemDto).ToList());
    }

    public async Task<BulkBatchSummaryDto> HandleGetBatchSummaryAsync(
        string batchId,
        CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Bulk batch '{batchId}' not found.");

        return MapToSummaryDto(batch);
    }

    private static BulkBatchSummaryDto MapToSummaryDto(BulkCorporateBatch b) =>
        new(
            b.Id,
            b.BatchType.ToString(),
            b.CorporateDebitAccountNumber.Value,
            b.Narration,
            b.ValueDate,
            b.Status.ToString(),
            b.TotalAmount.Amount,
            b.TotalRecords,
            b.SuccessCount,
            b.FailedCount,
            b.SuccessfulAmount.Amount,
            b.FailedAmount.Amount,
            b.CreatedByUserId,
            b.AuthorizedByUserId,
            b.CreatedAtUtc,
            b.ExecutedAtUtc,
            b.Items.Select(MapToItemDto).ToList());

    private static BulkBatchItemDto MapToItemDto(BulkCorporateBatchItem item) =>
        new(
            item.ItemIndex,
            item.BeneficiaryAccountNumber,
            item.BeneficiaryName,
            item.Amount.Amount,
            item.ReferenceNo,
            item.Status.ToString(),
            item.FailureReason,
            item.TransactionJournalId);
}
