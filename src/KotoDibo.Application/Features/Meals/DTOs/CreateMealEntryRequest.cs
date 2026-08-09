namespace KotoDibo.Application.Features.Meals.DTOs;

public record CreateMealEntryRequest
{
    public string UserId { get; init; } = string.Empty;
    public DateOnly Date { get; init; } = default;
    public decimal Count { get; init; } = default;
}
