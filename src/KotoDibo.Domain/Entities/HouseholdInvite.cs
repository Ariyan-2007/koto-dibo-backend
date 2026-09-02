using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// A redeemable credential, not a membership row. Accepting one CREATES a HouseholdMembership — it
// doesn't reactivate or presuppose one — which keeps the membership collection's "one active stint
// per user/household" invariant untouched by invite lifecycle. Email is who the inviter intended to
// reach (used to address the notification email and shown in previews), not an access control on
// redemption: the Code is the only thing Accept checks, so a forwarded code/QR still works exactly
// like the docs promise ("send the code through whatever means").
public class HouseholdInvite
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string InvitedByUserId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public string? Email { get; set; }

    public HouseholdInviteStatus Status { get; set; } = HouseholdInviteStatus.Pending;

    // Populated once the QR PNG has been generated and uploaded to R2; null only in the brief
    // window inside CreateAsync before that upload completes.
    public string? QrCodeUrl { get; set; }
    public string InviteLink { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
