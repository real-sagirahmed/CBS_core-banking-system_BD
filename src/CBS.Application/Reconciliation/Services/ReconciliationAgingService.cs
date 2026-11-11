using CBS.Application.Common.Interfaces;
using CBS.Application.Reconciliation.DTOs;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Reconciliation.Services;

// ARCHITECTURAL INTENT: Multi-Channel Unreconciled Transaction Aging & Statutory Provision Assessment Service
// conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 1.4.1.
public class ReconciliationAgingService
{
    private readonly IReconciliationAgingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReconciliationAgingService(IReconciliationAgingRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ReconciliationAgingItemResponse> RecordUnreconciledItemAsync(
        RecordUnreconciledItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var amount = Money.Bdt(request.TransactionAmount);
        var item = new ReconciliationAgingItem(
            Guid.NewGuid(),
            request.ChannelType,
            request.TraceOrReferenceNumber,
            request.OriginatingBranchCode,
            request.RespondingBranchOrCounterparty,
            amount,
            request.TransactionDate,
            request.AssessmentDate);

        await _repository.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(item);
    }

    public async Task<ReconciliationAgingItemResponse> ResolveItemAsync(
        ResolveUnreconciledItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByTraceNumberAsync(request.TraceOrReferenceNumber, cancellationToken)
            ?? throw new KeyNotFoundException($"Unreconciled transaction '{request.TraceOrReferenceNumber}' not found.");

        item.MarkResolved(request.ResolutionReference, request.ResolutionDate);

        await _repository.UpdateAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(item);
    }

    public async Task<ReconciliationAgingSummaryResponse> GetAgingSummaryAsync(
        DateOnly assessmentDate,
        string? channelType = null,
        CancellationToken cancellationToken = default)
    {
        var unresolvedItems = await _repository.GetUnresolvedItemsAsync(channelType, cancellationToken);

        // Update aging for each unresolved item as of assessment date
        foreach (var item in unresolvedItems)
        {
            item.UpdateAging(assessmentDate);
            await _repository.UpdateAsync(item, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Group into statutory buckets (BRPD 1.4.1-ii, iv)
        var allBuckets = Enum.GetValues<ReconciliationAgingBucket>();
        var bucketSummaries = new List<ReconciliationBucketSummary>();

        foreach (var b in allBuckets)
        {
            var inBucket = unresolvedItems.Where(i => i.Bucket == b).ToList();
            int count = inBucket.Count;
            decimal totalAmt = inBucket.Sum(i => i.TransactionAmount.Amount);
            decimal pct = b switch
            {
                ReconciliationAgingBucket.Days0To30 => 0.0m,
                ReconciliationAgingBucket.Days31To60 => 10.0m,
                ReconciliationAgingBucket.Days61To90 => 25.0m,
                ReconciliationAgingBucket.Days91To180 => 50.0m,
                _ => 100.0m
            };
            decimal provAmt = inBucket.Sum(i => i.RequiredProvisionAmount.Amount);

            bucketSummaries.Add(new ReconciliationBucketSummary(
                b.ToString(),
                count,
                totalAmt,
                pct,
                provAmt));
        }

        return new ReconciliationAgingSummaryResponse(
            assessmentDate,
            unresolvedItems.Count,
            unresolvedItems.Sum(i => i.TransactionAmount.Amount),
            unresolvedItems.Sum(i => i.RequiredProvisionAmount.Amount),
            bucketSummaries);
    }

    private static ReconciliationAgingItemResponse MapToResponse(ReconciliationAgingItem item)
    {
        return new ReconciliationAgingItemResponse(
            item.Id,
            item.ChannelType,
            item.TraceOrReferenceNumber,
            item.OriginatingBranchCode,
            item.RespondingBranchOrCounterparty,
            item.TransactionAmount.Amount,
            item.TransactionDate,
            item.LastAssessmentDate,
            item.AgeInDays,
            item.Bucket.ToString(),
            item.RequiredProvisionPercentage,
            item.RequiredProvisionAmount.Amount,
            item.IsResolved,
            item.ResolvedDate,
            item.ResolutionReference);
    }
}
