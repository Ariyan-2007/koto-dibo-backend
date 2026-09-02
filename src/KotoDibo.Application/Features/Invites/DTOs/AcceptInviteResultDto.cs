using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Invites.DTOs;

public record AcceptInviteResultDto
{
    public string HouseholdId { get; init; } = string.Empty;
    public string HouseholdName { get; init; } = string.Empty;
    public HouseholdMemberDto Member { get; init; } = new();
}
