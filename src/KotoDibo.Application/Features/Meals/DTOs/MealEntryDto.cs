namespace KotoDibo.Application.Features.Meals.DTOs;

public record MealEntryDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; } = default;
    public decimal Count { get; init; } = default;
}
