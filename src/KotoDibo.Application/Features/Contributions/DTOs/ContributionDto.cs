namespace KotoDibo.Application.Features.Contributions.DTOs;

public record ContributionDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string ContributedByUserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
