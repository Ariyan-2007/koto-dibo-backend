namespace KotoDibo.Domain.Entities;

public class MealEntry
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Count { get; set; }
}
