namespace KotoDibo.Domain.Enums;

// Household-scoped roles — distinct from the global application roles that will eventually live
// in the Identity subsystem's RBAC. A user's household role has no bearing on their global
// permissions, and vice versa.
public enum HouseholdRole
{
    Owner,
    Manager,
    Member,
    Viewer,
}
