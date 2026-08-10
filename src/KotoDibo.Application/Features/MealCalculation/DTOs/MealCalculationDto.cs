namespace KotoDibo.Application.Features.MealCalculation.DTOs;

public record MealMemberCostDto
{
    public string UserId { get; init; } = string.Empty;
    public decimal MealUnits { get; init; }
    public decimal MealCost { get; init; }
    public decimal Contribution { get; init; }
    public decimal GiveTake { get; init; }
}

public record MealCalculationDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public decimal FoodCost { get; init; }
    public decimal TotalMealUnits { get; init; }
    public decimal? MealRate { get; init; }
    public decimal TotalContributions { get; init; }
    public IReadOnlyList<MealMemberCostDto> Members { get; init; } = [];
    public string CalculationVersion { get; init; } = "v1";
}
