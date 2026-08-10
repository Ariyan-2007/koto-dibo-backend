using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // Legacy single-household reference from the original scaffold. Superseded by a proper
    // many-to-many Household Membership collection in a later phase; left untouched here.
    public string? HouseholdId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
