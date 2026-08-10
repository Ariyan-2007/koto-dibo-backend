using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Policies;

// What each household role is allowed to do, independent of any specific target/resource. Callers
// that need target-aware rules (e.g. "a Manager can't remove another Manager", "only Owner can be
// reassigned via ownership transfer, never via the generic role-update endpoint") layer those on
// top in the service that already knows the target — this policy only answers the role-level
// question, matching how ASP.NET Core policies vs. resource-based checks are usually split.
public static class HouseholdRolePolicy
{
    private static readonly IReadOnlyDictionary<HouseholdRole, HouseholdPermission[]> Permissions = new Dictionary<HouseholdRole, HouseholdPermission[]>
    {
        [HouseholdRole.Owner] =
        [
            HouseholdPermission.ViewHousehold, HouseholdPermission.UpdateHousehold,
            HouseholdPermission.ArchiveHousehold, HouseholdPermission.RestoreHousehold,
            HouseholdPermission.ViewMembers, HouseholdPermission.AddMember,
            HouseholdPermission.RemoveMember, HouseholdPermission.UpdateMemberRole,
            HouseholdPermission.LeaveHousehold,
        ],
        [HouseholdRole.Manager] =
        [
            HouseholdPermission.ViewHousehold, HouseholdPermission.UpdateHousehold,
            HouseholdPermission.ViewMembers, HouseholdPermission.AddMember,
            HouseholdPermission.RemoveMember, HouseholdPermission.LeaveHousehold,
        ],
        [HouseholdRole.Member] =
        [
            HouseholdPermission.ViewHousehold, HouseholdPermission.ViewMembers,
            HouseholdPermission.LeaveHousehold,
        ],
        [HouseholdRole.Viewer] =
        [
            HouseholdPermission.ViewHousehold, HouseholdPermission.ViewMembers,
            HouseholdPermission.LeaveHousehold,
        ],
    };

    public static bool HasPermission(HouseholdRole role, HouseholdPermission permission)
        => Permissions.TryGetValue(role, out var granted) && granted.Contains(permission);
}
