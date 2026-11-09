namespace CBS.Domain.Enums;

public enum UserRole
{
    SystemAdmin = 1,
    BranchTeller = 2,
    CadOfficer = 3,
    TradeMaker = 4,
    TradeChecker = 5,
    TreasuryDealer = 6,
    MidOfficeRiskOfficer = 7,
    BackOfficeSettlement = 8,
    ComplianceAuditor = 9
}

public enum AuditEventCategory
{
    UserManagement = 1,
    FinancialTransaction = 2,
    ParameterChange = 3,
    SecurityOverride = 4,
    SystemConfiguration = 5
}

public enum StrAlertReason
{
    StructuringSmurfing = 1,
    TransactionProfileExceeded = 2,
    SanctionWatchlistHit = 3,
    HighRiskJurisdiction = 4
}

public enum ComplianceReportStatus
{
    Generated = 1,
    Validated = 2,
    SubmittedToGoAml = 3
}
