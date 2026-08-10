namespace KotoDibo.Application.Features.Households.DTOs;

public record HouseholdMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime JoinedAt { get; init; }
}
