using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Remittance;
using CBS.Domain.Reporting;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

// ARCHITECTURAL INTENT: Unit tests locking Wage Earners' 2.5% Remittance Cash Incentive,
// Branch Daily Clean Cash Proofing, and Statutory Reporting Invariants
// per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 11 and FE Circular No. 01/2022.

public class EnterpriseReportingUnitTests
{
    // =========================================================================
    // 1. Wage Earners' Remittance 2.5% Government Cash Incentive Tests
    // =========================================================================

    [Fact]
    public void RemittanceIncentive_Should_Calculate_Exact_2_5_Percent_And_Total_Disbursed()
    {
        // ARCHITECTURAL INTENT: RemittanceIncentiveCalculationInvariant mandates exact 2.5%
        // cash incentive calculation and double-entry balanced disbursement.
        var record = new RemittanceCashIncentiveRecord(
            incentiveId: "INC-20261112-001",
            remittanceReferenceNumber: "EXCH-NYC-889911",
            exchangeHouseCode: "PL-EXCHANGE-NY",
            remitterName: "Tanvir Ahmed",
            remitterCountry: "USA",
            beneficiaryAccountNumber: "0100123456789",
            beneficiaryCif: "CIF-554433",
            remittanceAmountBdt: Money.Bdt(100_000m),
            disbursingBranchCode: "BR-001",
            disbursedByUserId: "TELLER-01",
            disbursedAtUtc: DateTime.UtcNow);

        record.IncentiveRate.Should().Be(0.025m);
        record.IncentiveAmountBdt.Amount.Should().Be(2_500.00m);
        record.TotalDisbursedBdt.Amount.Should().Be(102_500.00m);
        record.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.PendingClaimSubmission);
    }

    [Fact]
    public void RemittanceIncentive_Should_Handle_Decimal_Rounding_Precision()
    {
        // 35,425 BDT * 0.025 = 885.625 BDT -> 885.63 BDT (AwayFromZero)
        var record = new RemittanceCashIncentiveRecord(
            incentiveId: "INC-20261112-002",
            remittanceReferenceNumber: "EXCH-LON-112233",
            exchangeHouseCode: "SONALI-EXCH-UK",
            remitterName: "Mohammad Ali",
            remitterCountry: "GBR",
            beneficiaryAccountNumber: "0100987654321",
            beneficiaryCif: "CIF-998877",
            remittanceAmountBdt: Money.Bdt(35_425m),
            disbursingBranchCode: "BR-002",
            disbursedByUserId: "TELLER-02",
            disbursedAtUtc: DateTime.UtcNow);

        record.IncentiveAmountBdt.Amount.Should().Be(885.63m);
        record.TotalDisbursedBdt.Amount.Should().Be(36_310.63m);
    }

    [Fact]
    public void RemittanceIncentive_Should_Reject_Zero_Or_Negative_Amount()
    {
        var actZero = () => new RemittanceCashIncentiveRecord(
            incentiveId: "INC-INVALID-01",
            remittanceReferenceNumber: "EXCH-ZERO",
            exchangeHouseCode: "EXCH-01",
            remitterName: "Test Remitter",
            remitterCountry: "USA",
            beneficiaryAccountNumber: "0100000000001",
            beneficiaryCif: "CIF-001",
            remittanceAmountBdt: Money.Bdt(0m),
            disbursingBranchCode: "BR-001",
            disbursedByUserId: "TELLER-01",
            disbursedAtUtc: DateTime.UtcNow);

        actZero.Should().Throw<InvariantViolationException>()
            .WithMessage("*RemittanceIncentiveCalculationInvariant*")
            .WithMessage("*strictly greater than zero*");
    }

    [Fact]
    public void RemittanceIncentive_Claim_Lifecycle_And_Reimbursement_Flow()
    {
        var record = new RemittanceCashIncentiveRecord(
            incentiveId: "INC-20261112-003",
            remittanceReferenceNumber: "EXCH-DXB-445566",
            exchangeHouseCode: "AL-ANSARI-UAE",
            remitterName: "Kamal Hossain",
            remitterCountry: "ARE",
            beneficiaryAccountNumber: "0100555555555",
            beneficiaryCif: "CIF-332211",
            remittanceAmountBdt: Money.Bdt(200_000m),
            disbursingBranchCode: "BR-001",
            disbursedByUserId: "TELLER-01",
            disbursedAtUtc: DateTime.UtcNow);

        // 1. Submit claim schedule
        record.SubmitClaimToBangladeshBank("BB-CLAIM-BATCH-202611-01");
        record.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.ClaimSubmittedToCentralBank);
        record.ClaimBatchReference.Should().Be("BB-CLAIM-BATCH-202611-01");
        record.ClaimSubmittedAtUtc.Should().NotBeNull();

        // 2. Double submit must fail
        var doubleSubmit = () => record.SubmitClaimToBangladeshBank("BB-CLAIM-BATCH-202611-02");
        doubleSubmit.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot submit claim in status*");

        // 3. Central Bank reimbursement
        record.RecordCentralBankReimbursement("BB-FEOD-VOUCHER-98765");
        record.ClaimStatus.Should().Be(RemittanceIncentiveClaimStatus.ReimbursedByCentralBank);
        record.CentralBankVoucherReference.Should().Be("BB-FEOD-VOUCHER-98765");
        record.ReimbursedAtUtc.Should().NotBeNull();
    }

    // =========================================================================
    // 2. Branch Daily Clean Cash & Proofing Tests
    // =========================================================================

    [Fact]
    public void DailyCleanCash_Should_Proof_When_Physical_Cash_Equals_Calculated_Cash()
    {
        // ARCHITECTURAL INTENT: DailyCleanCashProofInvariant verifies that physical cash in hand
        // strictly equals Opening Cash + Cash Inflows - Cash Outflows (BRPD 11.1.1-a, 11.1.1-h).
        // Opening: 30M vault + 10M teller = 40M.
        // Received: 15M. Paid: 12M.
        // Calculated Closing = (40M + 15M) - 12M = 43M.
        // Physical Closing = 43M -> Variance = 0.
        var sheet = new DailyCleanCashSheet(
            sheetId: "CC-BR001-20261112",
            branchCode: "BR-001",
            operatingDate: new DateOnly(2026, 11, 12),
            openingVaultCash: Money.Bdt(30_000_000m),
            openingTellerCash: Money.Bdt(10_000_000m),
            totalCashReceived: Money.Bdt(15_000_000m),
            totalCashPaid: Money.Bdt(12_000_000m),
            physicalClosingCash: Money.Bdt(43_000_000m),
            createdAtUtc: DateTime.UtcNow);

        sheet.CashDifference.Amount.Should().Be(0m);
        sheet.CalculatedClosingCash.Amount.Should().Be(43_000_000m);

        sheet.CertifyAndProofCleanCash("HEAD-CASHIER-01", "BRANCH-MANAGER-01");

        sheet.ProofStatus.Should().Be(CleanCashProofStatus.BalancedInProof);
        sheet.CertifiedByHeadCashierUserId.Should().Be("HEAD-CASHIER-01");
        sheet.ApprovedByManagerUserId.Should().Be("BRANCH-MANAGER-01");
        sheet.CertifiedAtUtc.Should().NotBeNull();
        sheet.DiscrepancyNotes.Should().BeNull();
    }

    [Fact]
    public void DailyCleanCash_Should_Reject_When_Physical_Cash_Does_Not_Tally()
    {
        // Calculated Closing = 43M. Physical Closing = 42,975,000 (Shortage of 25,000 BDT)
        var sheet = new DailyCleanCashSheet(
            sheetId: "CC-BR001-20261112-SHORT",
            branchCode: "BR-001",
            operatingDate: new DateOnly(2026, 11, 12),
            openingVaultCash: Money.Bdt(30_000_000m),
            openingTellerCash: Money.Bdt(10_000_000m),
            totalCashReceived: Money.Bdt(15_000_000m),
            totalCashPaid: Money.Bdt(12_000_000m),
            physicalClosingCash: Money.Bdt(42_975_000m),
            createdAtUtc: DateTime.UtcNow);

        sheet.CashDifference.Amount.Should().Be(-25_000m);

        var act = () => sheet.CertifyAndProofCleanCash("HEAD-CASHIER-01", "BRANCH-MANAGER-01");

        act.Should().Throw<InvariantViolationException>()
            .WithMessage("*DailyCleanCashProofInvariant*")
            .WithMessage("*Physical Cash (42,975,000.00) != Calculated Clean Cash (43,000,000.00)*");

        sheet.ProofStatus.Should().Be(CleanCashProofStatus.DiscrepancyDetected);
        sheet.DiscrepancyNotes.Should().Contain("Physical cash");
        sheet.ApprovedByManagerUserId.Should().BeNull();
    }
}
