using System;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Remittance;

// ARCHITECTURAL INTENT: Wage Earners' Inward Remittance 2.5% Government Cash Incentive Engine.
// Governed strictly by Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 11.1.5-g
// and Bangladesh Bank FE Circular No. 01/2022.
// Enforces mandatory 2.5% cash incentive calculation, beneficiary credit verification,
// and audit trail for reimbursement claims submitted to Bangladesh Bank Foreign Exchange Operation Department (FEOD).

public sealed class RemittanceCashIncentiveRecord : AggregateRoot<string>
{
    public const decimal StatutoryIncentiveRate = 0.025m; // Strictly 2.5%

    public string IncentiveId => Id;
    public string RemittanceReferenceNumber { get; private set; } = null!;
    public string ExchangeHouseCode { get; private set; } = null!;
    public string RemitterName { get; private set; } = null!;
    public string RemitterCountry { get; private set; } = null!;
    public string BeneficiaryAccountNumber { get; private set; } = null!;
    public string BeneficiaryCif { get; private set; } = null!;
    public Money RemittanceAmountBdt { get; private set; } = null!;
    public decimal IncentiveRate { get; private set; }
    public Money IncentiveAmountBdt { get; private set; } = null!;
    public Money TotalDisbursedBdt { get; private set; } = null!;
    public string DisbursingBranchCode { get; private set; } = null!;
    public DateTime DisbursedAtUtc { get; private set; }
    public string DisbursedByUserId { get; private set; } = null!;
    public RemittanceIncentiveClaimStatus ClaimStatus { get; private set; }
    public string? ClaimBatchReference { get; private set; }
    public DateTime? ClaimSubmittedAtUtc { get; private set; }
    public DateTime? ReimbursedAtUtc { get; private set; }
    public string? CentralBankVoucherReference { get; private set; }

    #pragma warning disable CS8618
    private RemittanceCashIncentiveRecord() : base(string.Empty) { }
    #pragma warning restore CS8618

    public RemittanceCashIncentiveRecord(
        string incentiveId,
        string remittanceReferenceNumber,
        string exchangeHouseCode,
        string remitterName,
        string remitterCountry,
        string beneficiaryAccountNumber,
        string beneficiaryCif,
        Money remittanceAmountBdt,
        string disbursingBranchCode,
        string disbursedByUserId,
        DateTime disbursedAtUtc) : base(incentiveId.Trim().ToUpperInvariant())
    {
        if (string.IsNullOrWhiteSpace(incentiveId))
            throw new ArgumentException("Incentive ID is required.", nameof(incentiveId));
        if (string.IsNullOrWhiteSpace(remittanceReferenceNumber))
            throw new ArgumentException("Remittance reference number is required.", nameof(remittanceReferenceNumber));
        if (string.IsNullOrWhiteSpace(exchangeHouseCode))
            throw new ArgumentException("Exchange house code is required.", nameof(exchangeHouseCode));
        if (string.IsNullOrWhiteSpace(remitterName))
            throw new ArgumentException("Remitter name is required.", nameof(remitterName));
        if (string.IsNullOrWhiteSpace(remitterCountry))
            throw new ArgumentException("Remitter country is required.", nameof(remitterCountry));
        if (string.IsNullOrWhiteSpace(beneficiaryAccountNumber))
            throw new ArgumentException("Beneficiary account number is required.", nameof(beneficiaryAccountNumber));
        if (string.IsNullOrWhiteSpace(beneficiaryCif))
            throw new ArgumentException("Beneficiary CIF is required.", nameof(beneficiaryCif));
        if (string.IsNullOrWhiteSpace(disbursingBranchCode))
            throw new ArgumentException("Disbursing branch code is required.", nameof(disbursingBranchCode));
        if (string.IsNullOrWhiteSpace(disbursedByUserId))
            throw new ArgumentException("Disbursing user ID is required.", nameof(disbursedByUserId));

        ArgumentNullException.ThrowIfNull(remittanceAmountBdt, nameof(remittanceAmountBdt));

        if (remittanceAmountBdt.Amount <= 0)
        {
            throw new InvariantViolationException(
                "RemittanceIncentiveCalculationInvariant",
                "Bangladesh Bank FE Circular 01/2022",
                "Inward foreign remittance amount must be strictly greater than zero to qualify for cash incentive.");
        }

        decimal calculatedIncentive = Math.Round(remittanceAmountBdt.Amount * StatutoryIncentiveRate, 2, MidpointRounding.AwayFromZero);
        var incentiveMoney = Money.Bdt(calculatedIncentive);

        RemittanceReferenceNumber = remittanceReferenceNumber.Trim();
        ExchangeHouseCode = exchangeHouseCode.Trim().ToUpperInvariant();
        RemitterName = remitterName.Trim();
        RemitterCountry = remitterCountry.Trim().ToUpperInvariant();
        BeneficiaryAccountNumber = beneficiaryAccountNumber.Trim();
        BeneficiaryCif = beneficiaryCif.Trim().ToUpperInvariant();
        RemittanceAmountBdt = remittanceAmountBdt;
        IncentiveRate = StatutoryIncentiveRate;
        IncentiveAmountBdt = incentiveMoney;
        TotalDisbursedBdt = remittanceAmountBdt + incentiveMoney;
        DisbursingBranchCode = disbursingBranchCode.Trim();
        DisbursedByUserId = disbursedByUserId.Trim();
        DisbursedAtUtc = disbursedAtUtc;
        ClaimStatus = RemittanceIncentiveClaimStatus.PendingClaimSubmission;
    }

    /// <summary>
    /// Submits reimbursement claim schedule to Bangladesh Bank FEOD.
    /// </summary>
    public void SubmitClaimToBangladeshBank(string claimBatchReference)
    {
        if (string.IsNullOrWhiteSpace(claimBatchReference))
            throw new ArgumentException("Claim batch reference is required.", nameof(claimBatchReference));

        if (ClaimStatus != RemittanceIncentiveClaimStatus.PendingClaimSubmission)
            throw new InvalidOperationException($"Cannot submit claim in status '{ClaimStatus}'.");

        ClaimStatus = RemittanceIncentiveClaimStatus.ClaimSubmittedToCentralBank;
        ClaimBatchReference = claimBatchReference.Trim();
        ClaimSubmittedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records reimbursement payment received from Bangladesh Bank.
    /// </summary>
    public void RecordCentralBankReimbursement(string centralBankVoucherReference)
    {
        if (string.IsNullOrWhiteSpace(centralBankVoucherReference))
            throw new ArgumentException("Central Bank voucher reference is required.", nameof(centralBankVoucherReference));

        if (ClaimStatus != RemittanceIncentiveClaimStatus.ClaimSubmittedToCentralBank)
            throw new InvalidOperationException($"Cannot record reimbursement in status '{ClaimStatus}'.");

        ClaimStatus = RemittanceIncentiveClaimStatus.ReimbursedByCentralBank;
        CentralBankVoucherReference = centralBankVoucherReference.Trim();
        ReimbursedAtUtc = DateTime.UtcNow;
    }
}
