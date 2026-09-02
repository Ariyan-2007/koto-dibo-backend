namespace KotoDibo.Application.Features.Invites.DTOs;

public record CreateHouseholdInviteRequest
{
    // Who the inviter intends this to reach — addresses the notification email and shows up in
    // GetPending for the inviter's own reference. Not required (an invite can be created purely to
    // generate a code/QR to hand over in person) and never checked against the account that
    // ultimately redeems the Code.
    public string? Email { get; init; }

    public string Role { get; init; } = string.Empty;

    // Defaults to IInviteSettings.DefaultExpiry (168h) when omitted; capped at MaxExpiry (720h).
    public int? ExpiresInHours { get; init; }
}
