using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// One document per (UserId, Provider) pair. Password today; Google/Apple/Phone become additional
// documents for the same user in the future, without changing User or this entity's shape.
public class UserCredential
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public AuthProvider Provider { get; set; } = AuthProvider.Password;

    public string? PasswordHash { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
