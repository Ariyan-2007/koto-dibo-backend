namespace KotoDibo.Application.Features.Households.DTOs;

public record HouseholdDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string Status { get; init; } = string.Empty;
    public string OwnerUserId { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public string CallerRole { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ArchivedAt { get; init; }
}
