namespace KotoDibo.Application.Features.Meals.DTOs;

public record SetMealCountRequest
{
    public decimal Count { get; init; }
    public string? Notes { get; init; }
}
