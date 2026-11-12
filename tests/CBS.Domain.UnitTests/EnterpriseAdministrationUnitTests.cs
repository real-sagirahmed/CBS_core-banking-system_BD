// ARCHITECTURAL INTENT: Domain Unit Tests locking Administrative SoD Financial Restriction,
// Concurrent Single-Session Terminal Integrity, Admin Maker-Checker Dual-Control,
// and Bank Global Parameters Governance per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.5-vii, viii, x; 10.3.2).

using System;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Security;
using FluentAssertions;
using Xunit;

namespace CBS.Domain.UnitTests;

public class EnterpriseAdministrationUnitTests
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("SUPER_ADMIN")]
    [InlineData("SystemAdmin")]
    [InlineData("ITAdmin")]
    [InlineData("DatabaseAdmin")]
    [InlineData("DBA")]
    public void AdminFinancialRestriction_Should_Block_Admin_Roles_From_Financial_Transactions(string adminRole)
    {
        // Act
        Action act = () => AdminFinancialRestrictionPolicy.EnsureNonAdminFinancialRole(adminRole, "PostFinancialVoucher");

        // Assert
        var ex = act.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("AdminFinancialTransactionProhibitedInvariant");
        ex.RegulatoryReference.Should().Contain("10.3.5-vii");
        ex.Message.Should().Contain("strictly prohibited from performing financial transaction");
    }

    [Theory]
    [InlineData("Teller")]
    [InlineData("BranchManager")]
    [InlineData("OperationManager")]
    [InlineData("CustomerRelationshipManager")]
    [InlineData("TreasuryDealer")]
    public void AdminFinancialRestriction_Should_Allow_Operational_Roles(string operationalRole)
    {
        // Act & Assert
        Action act = () => AdminFinancialRestrictionPolicy.EnsureNonAdminFinancialRole(operationalRole, "PostFinancialVoucher");
        act.Should().NotThrow();
    }

    [Fact]
    public void UserActiveSession_Should_Enforce_Terminal_Integrity_Invariant()
    {
        // Act & Assert: Empty Terminal IP
        Action emptyTerminal = () => new UserActiveSession("SESS-01", "USER-01", "", "192.168.1.10", "BR-001");
        emptyTerminal.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("SessionTerminalIntegrityInvariant");

        // Act & Assert: Empty App Server IP
        Action emptyAppServer = () => new UserActiveSession("SESS-01", "USER-01", "10.0.0.5", "   ", "BR-001");
        emptyAppServer.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("SessionTerminalIntegrityInvariant");
    }

    [Fact]
    public void UserActiveSession_Should_Track_Activity_And_Handle_Termination()
    {
        // Arrange
        var session = new UserActiveSession("SESS-TEST-01", "TELLER-01", "10.10.1.25", "10.10.2.1", "BR-001");
        session.IsActive.Should().BeTrue();
        session.UserId.Should().Be("TELLER-01");
        session.TerminalIp.Should().Be("10.10.1.25");

        // Act: Touch Activity
        var newTime = DateTime.UtcNow.AddMinutes(5);
        session.TouchActivity(newTime);
        session.LastActivityUtc.Should().Be(newTime);

        // Act: Terminate Session
        session.TerminateSession(SessionTerminationReason.UserLogout, "Normal teller logoff");

        // Assert
        session.IsActive.Should().BeFalse();
        session.TerminationReason.Should().Be(SessionTerminationReason.UserLogout);
        session.TerminationNotes.Should().Be("Normal teller logoff");
        session.TerminatedAtUtc.Should().NotBeNull();

        // Attempting to touch terminated session must throw SingleActiveSessionInvariant
        Action touchTerminated = () => session.TouchActivity(DateTime.UtcNow);
        touchTerminated.Should().Throw<InvariantViolationException>()
            .Which.InvariantName.Should().Be("SingleActiveSessionInvariant");
    }

    [Fact]
    public void AdminChangeRequest_Should_Prevent_Maker_Self_Approval()
    {
        // Arrange
        var request = new AdminChangeRequest(
            requestId: "CHG-001",
            category: AdminChangeCategory.BankGlobalParameters,
            changeDescription: "Update weekly holiday calendar",
            changePayloadJson: "{}",
            makerUserId: "ADMIN-MAKER-01",
            makerBranchCode: "HO-001");

        request.Status.Should().Be(AdminChangeStatus.PendingCheckerApproval);

        // Act & Assert: Maker attempting to approve own request
        Action selfApprove = () => request.Approve("ADMIN-MAKER-01", "Approving my own config");
        var ex = selfApprove.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("AdminMakerCheckerInvariant");
        ex.RegulatoryReference.Should().Contain("10.3.5-x");
        ex.Message.Should().Contain("cannot approve their own administrative change request");
    }

    [Fact]
    public void AdminChangeRequest_Should_Prevent_Maker_Self_Rejection()
    {
        // Arrange
        var request = new AdminChangeRequest(
            requestId: "CHG-002",
            category: AdminChangeCategory.SystemOperationalFlag,
            changeDescription: "Toggle settlement flag",
            changePayloadJson: "{}",
            makerUserId: "ADMIN-MAKER-01",
            makerBranchCode: "HO-001");

        // Act & Assert: Maker attempting to reject own request
        Action selfReject = () => request.Reject("admin-maker-01", "Rejecting my own request");
        var ex = selfReject.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("AdminMakerCheckerInvariant");
        ex.RegulatoryReference.Should().Contain("10.3.5-x");
    }

    [Fact]
    public void AdminChangeRequest_Should_Allow_Independent_Checker_Approval_And_Rejection()
    {
        // 1. Approval flow
        var approveReq = new AdminChangeRequest(
            requestId: "CHG-003",
            category: AdminChangeCategory.BankGlobalParameters,
            changeDescription: "Update parameters",
            changePayloadJson: "{}",
            makerUserId: "MAKER-01",
            makerBranchCode: "HO-001");

        approveReq.Approve("CHECKER-02", "Dual control verified");
        approveReq.Status.Should().Be(AdminChangeStatus.Approved);
        approveReq.CheckerUserId.Should().Be("CHECKER-02");
        approveReq.CheckerRemarks.Should().Be("Dual control verified");
        approveReq.ActionedAtUtc.Should().NotBeNull();

        // 2. Rejection flow
        var rejectReq = new AdminChangeRequest(
            requestId: "CHG-004",
            category: AdminChangeCategory.UserEntitlementUpdate,
            changeDescription: "Update user roles",
            changePayloadJson: "{}",
            makerUserId: "MAKER-01",
            makerBranchCode: "HO-001");

        rejectReq.Reject("CHECKER-02", "Insufficient justification provided");
        rejectReq.Status.Should().Be(AdminChangeStatus.Rejected);
        rejectReq.CheckerUserId.Should().Be("CHECKER-02");
        rejectReq.RejectionReason.Should().Be("Insufficient justification provided");
    }

    [Fact]
    public void BankGlobalParameters_Should_Enforce_BaseCurrency_BDT()
    {
        // Act & Assert
        Action nonBdt = () => new BankGlobalParameters(
            bankCode: "BD-CBS-001",
            bankName: "Sonar Bangla Bank",
            baseCurrencyCode: "USD", // Non-BDT prohibited
            localCurrencyCode: "BDT",
            chiefLocalOfficeBranchCode: "BR-001",
            weeklyHolidaysCsv: "Friday,Saturday",
            fiscalYearStartMonth: 7,
            fiscalYearStartDay: 1,
            realTimeBranchSettlementEnabled: true,
            allowAlphaCustomerNumber: false,
            maxExternalAccountNumberLength: 16,
            dailySpotRateListDetailsJson: null,
            lastUpdatedByUserId: "ADMIN-01");

        var ex = nonBdt.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BankParametersGovernanceInvariant");
        ex.RegulatoryReference.Should().Contain("10.3.2-e, f");
        ex.Message.Should().Contain("Base currency must be 'BDT'");
    }

    [Fact]
    public void BankGlobalParameters_Should_Enforce_Friday_In_Weekly_Holidays()
    {
        // Act & Assert
        Action noFriday = () => new BankGlobalParameters(
            bankCode: "BD-CBS-001",
            bankName: "Sonar Bangla Bank",
            baseCurrencyCode: "BDT",
            localCurrencyCode: "BDT",
            chiefLocalOfficeBranchCode: "BR-001",
            weeklyHolidaysCsv: "Saturday,Sunday", // Missing Friday
            fiscalYearStartMonth: 7,
            fiscalYearStartDay: 1,
            realTimeBranchSettlementEnabled: true,
            allowAlphaCustomerNumber: false,
            maxExternalAccountNumberLength: 16,
            dailySpotRateListDetailsJson: null,
            lastUpdatedByUserId: "ADMIN-01");

        var ex = noFriday.Should().Throw<InvariantViolationException>().Which;
        ex.InvariantName.Should().Be("BankParametersGovernanceInvariant");
        ex.RegulatoryReference.Should().Contain("10.3.2-b");
        ex.Message.Should().Contain("designate at least 'Friday'");
    }
}
