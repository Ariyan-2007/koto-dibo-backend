namespace KotoDibo.Domain.Enums;

// Invited/Suspended are intentionally not modeled yet — Invited requires the token-based
// invitation flow (a separate future phase) to have a code path that ever produces it, and
// per-member suspension has no caller yet either. Adding either later is additive.
public enum HouseholdMembershipStatus
{
    Active,
    Left,
    Removed,
}
