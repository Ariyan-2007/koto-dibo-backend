namespace KotoDibo.Domain.Entities;

public class MealRate
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal RatePerMeal { get; set; }
}
