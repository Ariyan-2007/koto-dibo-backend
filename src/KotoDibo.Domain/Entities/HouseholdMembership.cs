using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// One document per "stint": a user joining, and eventually leaving/being removed, produces one
// immutable-once-closed record. Rejoining after leaving creates a NEW document rather than
// reactivating the old one, so history stays a clean append-only trail (see MongoDb index notes
// for how a partial unique index still guarantees at most one Active stint per user/household).
public class HouseholdMembership
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public HouseholdMembershipStatus Status { get; set; } = HouseholdMembershipStatus.Active;

    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
