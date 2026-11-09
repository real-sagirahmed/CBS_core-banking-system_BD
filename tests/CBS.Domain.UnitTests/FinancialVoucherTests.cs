using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.GeneralLedger;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.UnitTests;

public class FinancialVoucherTests
{
    [Fact]
    public void AuthorizeAndPost_WithBalancedVoucherAndDifferentChecker_ShouldSucceed()
    {
        // Arrange
        var voucher = new FinancialVoucher(
            voucherNumber: "VCH-2026-0001",
            branchSol: "0101",
            type: VoucherType.Transfer,
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            makerUserId: "USER_MAKER_01");

        voucher.AddEntry("10101", DebitCredit.Debit, new Money(50000m, Currency.BDT), 1.0m, "Debit Cash");
        voucher.AddEntry("20101", DebitCredit.Credit, new Money(50000m, Currency.BDT), 1.0m, "Credit Customer Deposit");

        // Act
        voucher.AuthorizeAndPost("USER_CHECKER_02");

        // Assert
        Assert.Equal(VoucherStatus.Posted, voucher.Status);
        Assert.Equal("USER_CHECKER_02", voucher.Audit.CheckerUserId);
        Assert.Equal(MakerCheckerStatus.Approved, voucher.Audit.Status);
    }

    [Fact]
    public void AuthorizeAndPost_WhenDebitsDoNotEqualCredits_MustThrowInvariantViolationException()
    {
        // Arrange
        var voucher = new FinancialVoucher(
            voucherNumber: "VCH-2026-0002",
            branchSol: "0101",
            type: VoucherType.Transfer,
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            makerUserId: "USER_MAKER_01");

        // Debit 50,000 but Credit only 40,000 (Mismatched contra balance)
        voucher.AddEntry("10101", DebitCredit.Debit, new Money(50000m, Currency.BDT), 1.0m, "Debit Cash");
        voucher.AddEntry("20101", DebitCredit.Credit, new Money(40000m, Currency.BDT), 1.0m, "Credit Deposit");

        // Act & Assert
        var ex = Assert.Throws<InvariantViolationException>(() => voucher.AuthorizeAndPost("USER_CHECKER_02"));
        Assert.Equal("SelfBalancingGeneralLedgerInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.3.5-i, 1.3.13-i, 1.3.13-ii)", ex.RegulatoryReference);
    }

    [Fact]
    public void AuthorizeAndPost_WhenCheckerIsSameAsMaker_MustThrowInvariantViolationException()
    {
        // Arrange
        const string sameUser = "OFFICER_RAHIM";
        var voucher = new FinancialVoucher(
            voucherNumber: "VCH-2026-0003",
            branchSol: "0101",
            type: VoucherType.Cash,
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            makerUserId: sameUser);

        voucher.AddEntry("10101", DebitCredit.Debit, new Money(1000m, Currency.BDT), 1.0m, "Debit");
        voucher.AddEntry("20101", DebitCredit.Credit, new Money(1000m, Currency.BDT), 1.0m, "Credit");

        // Act & Assert (Rahim trying to approve his own voucher)
        var ex = Assert.Throws<InvariantViolationException>(() => voucher.AuthorizeAndPost(sameUser));
        Assert.Equal("MakerCheckerSeparationInvariant", ex.InvariantName);
        Assert.Contains("BRPD Circular 12/2024 (1.3.18-i, 9.1.1-xii, 10.3.5-x)", ex.RegulatoryReference);
    }

    [Fact]
    public void CreateReversal_FromPostedVoucher_ShouldProduceInvertedBalancedVoucher()
    {
        // Arrange
        var original = new FinancialVoucher(
            voucherNumber: "VCH-ORIG-01",
            branchSol: "0101",
            type: VoucherType.Transfer,
            valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            makerUserId: "USER_MAKER_01");

        original.AddEntry("GL_DEBIT", DebitCredit.Debit, new Money(25000m, Currency.BDT), 1.0m, "Original Debit");
        original.AddEntry("GL_CREDIT", DebitCredit.Credit, new Money(25000m, Currency.BDT), 1.0m, "Original Credit");
        original.AuthorizeAndPost("USER_CHECKER_02");

        // Act
        var reversal = original.CreateReversal("VCH-REV-01", "USER_MAKER_03", "Posting error correction");

        // Assert
        Assert.Equal(VoucherStatus.Reversed, original.Status);
        Assert.Equal(VoucherType.Reversal, reversal.Type);
        Assert.Equal(original.Id, reversal.ReversalOfVoucherId);

        // Verify entries are inverted
        var revDebit = reversal.Entries.First(e => e.AccountCode == "GL_CREDIT");
        var revCredit = reversal.Entries.First(e => e.AccountCode == "GL_DEBIT");

        Assert.Equal(DebitCredit.Debit, revDebit.Type);
        Assert.Equal(25000m, revDebit.Amount.Amount);

        Assert.Equal(DebitCredit.Credit, revCredit.Type);
        Assert.Equal(25000m, revCredit.Amount.Amount);

        // Authorize reversal with a different checker
        reversal.AuthorizeAndPost("USER_CHECKER_04");
        Assert.Equal(VoucherStatus.Posted, reversal.Status);
    }
}
