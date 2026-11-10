using CBS.Domain.Adc;
using CBS.Domain.Enums;
using CBS.Domain.Reconciliation;
using CBS.Domain.ValueObjects;
using FluentAssertions;

namespace CBS.Domain.UnitTests;

public class AdcAndReconciliationTests
{
    [Fact]
    public void AdcTransaction_Creation_ValidatesStanExact6Digits()
    {
        var act1 = () => AdcTransaction.Create(
            "TX-001",
            AdcChannelType.Atm,
            "ATM-01",
            "4111222233334444",
            "12345", // Only 5 digits
            "202611100001",
            AccountNumber.From("0101101000010"),
            Money.Bdt(5000m),
            "ATM Withdrawal");

        act1.Should().Throw<ArgumentException>()
            .WithMessage("*Security Invariant Violation [BRPD 7.2.1-ii]: STAN must be exactly 6 numeric digits*");

        var act2 = () => AdcTransaction.Create(
            "TX-001",
            AdcChannelType.Atm,
            "ATM-01",
            "4111222233334444",
            "123456", // Valid 6 digits
            "202611100001",
            AccountNumber.From("0101101000010"),
            Money.Bdt(5000m),
            "ATM Withdrawal");

        act2.Should().NotThrow();
    }

    [Fact]
    public void AdcTransaction_Creation_ValidatesRrnExact12Chars()
    {
        var act = () => AdcTransaction.Create(
            "TX-001",
            AdcChannelType.Pos,
            "POS-01",
            "4111222233334444",
            "123456",
            "RRN123", // Short RRN
            AccountNumber.From("0101101000010"),
            Money.Bdt(2000m),
            "POS Purchase");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Security Invariant Violation [BRPD 7.2.1-ii]: RRN must be exactly 12 alphanumeric characters*");
    }

    [Fact]
    public void AdcTransaction_Creation_MasksCardNumber_PciDssCompliance()
    {
        var tx = AdcTransaction.Create(
            "TX-002",
            AdcChannelType.InternetBanking,
            "IB-WEB",
            "4111222233334444",
            "654321",
            "202611100002",
            AccountNumber.From("0101101000010"),
            Money.Bdt(10000m),
            "Utility Bill Payment");

        tx.CardNumberMasked.Should().Be("4111-****-****-4444");
        tx.CardNumberMasked.Should().NotContain("22223333");
    }

    [Fact]
    public void AtmTerminal_DispenseCash_UpdatesPhysicalVaultBalance()
    {
        var terminal = new AtmTerminal("ATM-01", "Dhaka Main Branch ATM", "0101", "Dhaka", 50000m, 100000m);
        terminal.CurrentCashBalance.Should().Be(50000m);

        terminal.DispenseCash(20000m);
        terminal.CurrentCashBalance.Should().Be(30000m);

        var act = () => terminal.DispenseCash(40000m);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Security Invariant Violation [BRPD 7.2.1-vi]*insufficient physical cash*");
    }

    [Fact]
    public void DisputeClaim_ResolveWithRefund_ThrowsException_IfProvidedStanOrRrnDoesNotMatchOriginal()
    {
        var claim = DisputeClaim.LogClaim(
            "DSP-001",
            "ADC-TX-001",
            "104521",
            "202611100001",
            AccountNumber.From("0101101000010"),
            Money.Bdt(5000m),
            AdcChannelType.Atm,
            "Cash not dispensed");

        // Act with mismatching STAN
        var actMismatchStan = () => claim.ResolveWithRefund(
            "CHECKER01",
            "VCH-REF-001",
            "999999", // Wrong STAN
            "202611100001",
            "Approved refund");

        actMismatchStan.Should().Throw<InvalidOperationException>()
            .WithMessage("*Security Invariant Violation [BRPD 1.4.1-vii]: Dispute refund MUST utilize the exact original STAN and RRN*");

        // Act with mismatching RRN
        var actMismatchRrn = () => claim.ResolveWithRefund(
            "CHECKER01",
            "VCH-REF-001",
            "104521",
            "WRONGRRN1234", // Wrong RRN
            "Approved refund");

        actMismatchRrn.Should().Throw<InvalidOperationException>()
            .WithMessage("*Security Invariant Violation [BRPD 1.4.1-vii]: Dispute refund MUST utilize the exact original STAN and RRN*");
    }

    [Fact]
    public void DisputeClaim_ResolveWithRefund_Succeeds_WhenStanAndRrnMatchOriginal()
    {
        var claim = DisputeClaim.LogClaim(
            "DSP-002",
            "ADC-TX-002",
            "104522",
            "202611100002",
            AccountNumber.From("0101101000010"),
            Money.Bdt(2000m),
            AdcChannelType.Atm,
            "Cash not dispensed");

        claim.ResolveWithRefund(
            "CHECKER01",
            "VCH-REF-002",
            "104522",
            "202611100002",
            "Approved after EJ log review");

        claim.Status.Should().Be(DisputeStatus.RefundApproved);
        claim.RectificationVoucherNumber.Should().Be("VCH-REF-002");
        claim.ResolvedByOfficerId.Should().Be("CHECKER01");
        claim.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Iso8583MessageEngine_PackAndUnpack_PreservesAllDataElements()
    {
        var msg = new Iso8583Message
        {
            Mti = "0200"
        };
        msg.Fields[2] = "4111222233334444";
        msg.Fields[3] = "010000";
        msg.SetAmountDecimal(5000.00m);
        msg.Fields[11] = "104521";
        msg.Fields[37] = "202611100001";
        msg.Fields[41] = "ATM00101";

        string packed = Iso8583MessageEngine.Pack(msg);
        packed.Should().StartWith("0200");

        var unpacked = Iso8583MessageEngine.Unpack(packed);
        unpacked.Mti.Should().Be("0200");
        unpacked.Pan.Should().Be("4111222233334444");
        unpacked.ProcessingCode.Should().Be("010000");
        unpacked.GetAmountDecimal().Should().Be(5000.00m);
        unpacked.Stan.Should().Be("104521");
        unpacked.Rrn.Should().Be("202611100001");
        unpacked.TerminalId.Should().Be("ATM00101");
    }

    [Fact]
    public void Iso8583MessageEngine_BuildResponse_SetsMti0210AndResponseCode()
    {
        var req = new Iso8583Message
        {
            Mti = "0200"
        };
        req.Fields[11] = "104521";
        req.Fields[37] = "202611100001";
        req.SetAmountDecimal(1000m);

        var res = Iso8583MessageEngine.BuildResponse(req, "00");
        res.Mti.Should().Be("0210");
        res.Stan.Should().Be("104521");
        res.Rrn.Should().Be("202611100001");
        res.ResponseCode.Should().Be("00");
    }
}
