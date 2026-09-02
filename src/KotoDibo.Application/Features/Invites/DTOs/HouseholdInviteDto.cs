namespace KotoDibo.Application.Features.Invites.DTOs;

public record HouseholdInviteDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string InvitedByUserId { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string Status { get; init; } = string.Empty;

    public string InviteLink { get; init; } = string.Empty;
    public string? QrCodeUrl { get; init; }

    public DateTime ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
