namespace KotoDibo.Application.Features.HouseholdBalance.DTOs;

// Unlike MealCalculationDto/HouseholdSettlementDto, this is not period-bound (no From/To): the
// shared fund is real cash sitting in the household's pocket right now, so it is always computed
// over every Active record ever recorded, not a date window.
public record HouseholdBalanceDto
{
    public string HouseholdId { get; init; } = string.Empty;
    public decimal TotalContributions { get; init; }
    public decimal TotalSpentFromFund { get; init; }
    public decimal CurrentBalance { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime AsOf { get; init; }
}
