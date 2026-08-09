namespace KotoDibo.Application.Features.Households.DTOs;

public record HouseholdDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InviteCode { get; init; } = string.Empty;
}
