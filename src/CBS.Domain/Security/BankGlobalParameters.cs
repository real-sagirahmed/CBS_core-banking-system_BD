// ARCHITECTURAL INTENT: Bank Global Parameters aggregate managing core operational parameters,
// currency standards, weekly holiday schedules, and accounting boundaries per Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 10 (10.3.2-a..s).

using System;
using CBS.Domain.Common;
using CBS.Domain.Exceptions;

namespace CBS.Domain.Security;

public class BankGlobalParameters : AggregateRoot<string>
{
    public string BankCode => Id;
    public string BankName { get; private set; } = "Sonar Bangla Bank PLC";
    public string BaseCurrencyCode { get; private set; } = "BDT";
    public string LocalCurrencyCode { get; private set; } = "BDT";
    public string ChiefLocalOfficeBranchCode { get; private set; } = "BR-001";
    public string WeeklyHolidaysCsv { get; private set; } = "Friday,Saturday";
    public int FiscalYearStartMonth { get; private set; } = 7; // July in Bangladesh
    public int FiscalYearStartDay { get; private set; } = 1;
    public bool RealTimeBranchSettlementEnabled { get; private set; } = true;
    public bool AllowAlphaCustomerNumber { get; private set; } = false;
    public int MaxExternalAccountNumberLength { get; private set; } = 16;
    public string? DailySpotRateListDetailsJson { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }
    public string LastUpdatedByUserId { get; private set; } = "SYSTEM";

    // Concurrency tokens
    public byte[]? RowVersion { get; set; }
    public uint xmin { get; set; }

    // Private constructor for EF Core
    #pragma warning disable CS8618
    private BankGlobalParameters() : base(string.Empty) { }
    #pragma warning restore CS8618

    public BankGlobalParameters(
        string bankCode,
        string bankName,
        string baseCurrencyCode,
        string localCurrencyCode,
        string chiefLocalOfficeBranchCode,
        string weeklyHolidaysCsv,
        int fiscalYearStartMonth,
        int fiscalYearStartDay,
        bool realTimeBranchSettlementEnabled,
        bool allowAlphaCustomerNumber,
        int maxExternalAccountNumberLength,
        string? dailySpotRateListDetailsJson,
        string lastUpdatedByUserId,
        DateTime? lastUpdatedAtUtc = null) : base(bankCode?.Trim().ToUpperInvariant() ?? "BD-CBS-001")
    {
        ValidateAndAssign(
            bankCode,
            bankName,
            baseCurrencyCode,
            localCurrencyCode,
            chiefLocalOfficeBranchCode,
            weeklyHolidaysCsv,
            fiscalYearStartMonth,
            fiscalYearStartDay,
            realTimeBranchSettlementEnabled,
            allowAlphaCustomerNumber,
            maxExternalAccountNumberLength,
            dailySpotRateListDetailsJson,
            lastUpdatedByUserId,
            lastUpdatedAtUtc);
    }

    public static BankGlobalParameters CreateDefault(string updatedByUserId = "SYSTEM")
    {
        return new BankGlobalParameters(
            bankCode: "BD-CBS-001",
            bankName: "Sonar Bangla Bank PLC",
            baseCurrencyCode: "BDT",
            localCurrencyCode: "BDT",
            chiefLocalOfficeBranchCode: "BR-001",
            weeklyHolidaysCsv: "Friday,Saturday",
            fiscalYearStartMonth: 7,
            fiscalYearStartDay: 1,
            realTimeBranchSettlementEnabled: true,
            allowAlphaCustomerNumber: false,
            maxExternalAccountNumberLength: 16,
            dailySpotRateListDetailsJson: null,
            lastUpdatedByUserId: updatedByUserId,
            lastUpdatedAtUtc: DateTime.UtcNow);
    }

    public void UpdateParameters(
        string bankName,
        string baseCurrencyCode,
        string localCurrencyCode,
        string chiefLocalOfficeBranchCode,
        string weeklyHolidaysCsv,
        int fiscalYearStartMonth,
        int fiscalYearStartDay,
        bool realTimeBranchSettlementEnabled,
        bool allowAlphaCustomerNumber,
        int maxExternalAccountNumberLength,
        string? dailySpotRateListDetailsJson,
        string updatedByUserId,
        DateTime? updatedAtUtc = null)
    {
        ValidateAndAssign(
            BankCode,
            bankName,
            baseCurrencyCode,
            localCurrencyCode,
            chiefLocalOfficeBranchCode,
            weeklyHolidaysCsv,
            fiscalYearStartMonth,
            fiscalYearStartDay,
            realTimeBranchSettlementEnabled,
            allowAlphaCustomerNumber,
            maxExternalAccountNumberLength,
            dailySpotRateListDetailsJson,
            updatedByUserId,
            updatedAtUtc);
    }

    private void ValidateAndAssign(
        string bankCode,
        string bankName,
        string baseCurrencyCode,
        string localCurrencyCode,
        string chiefLocalOfficeBranchCode,
        string weeklyHolidaysCsv,
        int fiscalYearStartMonth,
        int fiscalYearStartDay,
        bool realTimeBranchSettlementEnabled,
        bool allowAlphaCustomerNumber,
        int maxExternalAccountNumberLength,
        string? dailySpotRateListDetailsJson,
        string updatedByUserId,
        DateTime? updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
            throw new ArgumentException("Bank code cannot be empty.", nameof(bankCode));

        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name cannot be empty.", nameof(bankName));

        // ARCHITECTURAL INTENT: Base currency must strictly be BDT for scheduled commercial banks in Bangladesh
        if (!string.Equals(baseCurrencyCode?.Trim(), "BDT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BankParametersGovernanceInvariant",
                "BRPD Circular 12/2024 (10.3.2-e, f)",
                $"Base currency must be 'BDT' per Bangladesh Bank regulations. Received: '{baseCurrencyCode}'.");
        }

        if (string.IsNullOrWhiteSpace(chiefLocalOfficeBranchCode))
        {
            throw new InvariantViolationException(
                "BankParametersGovernanceInvariant",
                "BRPD Circular 12/2024 (10.3.2-i)",
                "Chief Local Office branch code must be designated for bank operations and settlement.");
        }

        if (string.IsNullOrWhiteSpace(weeklyHolidaysCsv) || !weeklyHolidaysCsv.Contains("Friday", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvariantViolationException(
                "BankParametersGovernanceInvariant",
                "BRPD Circular 12/2024 (10.3.2-b)",
                "Weekly holiday parameters must designate at least 'Friday' in the weekend schedule for Bangladesh.");
        }

        if (fiscalYearStartMonth < 1 || fiscalYearStartMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth), "Fiscal year start month must be between 1 and 12.");

        if (fiscalYearStartDay < 1 || fiscalYearStartDay > 31)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartDay), "Fiscal year start day must be between 1 and 31.");

        if (maxExternalAccountNumberLength < 10 || maxExternalAccountNumberLength > 34)
            throw new ArgumentOutOfRangeException(nameof(maxExternalAccountNumberLength), "External account length must be between 10 and 34 characters.");

        Id = bankCode.Trim().ToUpperInvariant();
        BankName = bankName.Trim();
        BaseCurrencyCode = "BDT";
        LocalCurrencyCode = string.IsNullOrWhiteSpace(localCurrencyCode) ? "BDT" : localCurrencyCode.Trim().ToUpperInvariant();
        ChiefLocalOfficeBranchCode = chiefLocalOfficeBranchCode.Trim().ToUpperInvariant();
        WeeklyHolidaysCsv = weeklyHolidaysCsv.Trim();
        FiscalYearStartMonth = fiscalYearStartMonth;
        FiscalYearStartDay = fiscalYearStartDay;
        RealTimeBranchSettlementEnabled = realTimeBranchSettlementEnabled;
        AllowAlphaCustomerNumber = allowAlphaCustomerNumber;
        MaxExternalAccountNumberLength = maxExternalAccountNumberLength;
        DailySpotRateListDetailsJson = dailySpotRateListDetailsJson;
        LastUpdatedByUserId = string.IsNullOrWhiteSpace(updatedByUserId) ? "SYSTEM" : updatedByUserId.Trim().ToUpperInvariant();
        LastUpdatedAtUtc = updatedAtUtc ?? DateTime.UtcNow;
    }
}
