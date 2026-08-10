namespace KotoDibo.Application.Features.Meals.DTOs;

public record DailyMealEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public decimal Count { get; init; }
    public string? Notes { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
