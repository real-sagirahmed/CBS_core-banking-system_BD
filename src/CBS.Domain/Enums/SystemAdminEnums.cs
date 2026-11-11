namespace CBS.Domain.Enums;

public enum UserGroupType
{
    Administrator = 1,
    Manager = 2,
    OperationManager = 3,
    Supervisor = 4,
    FunctionalOfficer = 5,
    Teller = 6
}

public enum AdminActionType
{
    CreateUser = 1,
    UpdateUser = 2,
    ActivateUser = 3,
    DeactivateUser = 4,
    AssignRole = 5,
    UpdateDoaLimit = 6,
    UpdateSystemParameter = 7
}

public enum AdminProposalStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

public enum OverrideStatus
{
    Pending = 1,
    Authorized = 2,
    Declined = 3
}

public enum JournalCategory
{
    UserProfileActivity = 1,
    ConfigurationParameter = 2,
    DoaLimitOverride = 3
}

public enum UserAccountStatus
{
    Active = 1,
    Inactive = 2,
    Locked = 3,
    Suspended = 4
}
