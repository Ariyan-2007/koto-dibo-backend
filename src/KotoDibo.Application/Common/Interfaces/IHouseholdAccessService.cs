using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Common.Interfaces;

// The chokepoint every household-scoped operation goes through: authenticated user -> active
// household membership -> required permission -> resource access. Household services use this
// today; Meal/Expense/Bill/Budget services will depend on the same interface once they exist,
// rather than each reimplementing the membership+permission check.
public interface IHouseholdAccessService
{
    Task<HouseholdMembership> RequireMembershipAsync(string householdId, string userId, HouseholdPermission permission, CancellationToken cancellationToken = default);
}
