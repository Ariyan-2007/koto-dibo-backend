namespace KotoDibo.Application.Features.Invites.DTOs;

// Deliberately doesn't distinguish "code doesn't exist" from other failure modes via an exception —
// Status carries that (Pending/Accepted/Revoked/Expired) so the frontend can render a specific
// "already used" / "expired" / "revoked" message before the caller commits to accepting.
public record InvitePreviewDto
{
    public string Code { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string HouseholdName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string InvitedByName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }

    // True when the caller previewing this invite is already an active member of the household —
    // lets the frontend show "you're already in" instead of an Accept button.
    public bool CallerIsAlreadyMember { get; init; }
}
