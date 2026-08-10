namespace KotoDibo.Domain.Entities;

// A session/device grant. The raw token is never persisted — only its SHA-256 hash. FamilyId
// links a token to the chain it was rotated from, so a replayed/already-revoked token can trigger
// revocation of the entire chain (reuse detection).
public class RefreshToken
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;

    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedByIp { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
