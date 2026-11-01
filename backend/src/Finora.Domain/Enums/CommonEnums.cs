namespace Finora.Domain.Enums;

public enum HouseholdRole
{
    Owner = 1,
    Admin = 2,
    Member = 3,
    Viewer = 4
}

public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Revoked = 3,
    Expired = 4
}

public enum VisibilityLevel
{
    Household = 1,
    Private = 2,
    SelectedMembers = 3
}

public enum AccountType
{
    Savings = 1,
    Checking = 2,
    CreditCard = 3,
    Cash = 4,
    Loan = 5,
    Investment = 6
}

public enum TransactionType
{
    Expense = 1,
    Income = 2,
    Transfer = 3
}

