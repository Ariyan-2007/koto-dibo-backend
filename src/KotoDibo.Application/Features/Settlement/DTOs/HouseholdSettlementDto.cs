namespace KotoDibo.Application.Features.Settlement.DTOs;

public record HouseholdMemberSettlementDto
{
    public string UserId { get; init; } = string.Empty;
    public decimal MealGiveTake { get; init; }
    public decimal BillSplitOwed { get; init; }
    public decimal NetBalance { get; init; }
}

public record HouseholdSettlementDto
{
    public string HouseholdId { get; init; } = string.Empty;
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public decimal TotalMealGiveTake { get; init; }
    public decimal TotalBillSplitOwed { get; init; }
    public IReadOnlyList<HouseholdMemberSettlementDto> Members { get; init; } = [];
    public string CalculationVersion { get; init; } = "v1";
}
