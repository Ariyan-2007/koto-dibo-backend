using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// One row per household/user/date — how many meals that member ate that day, total (not split by
// breakfast/lunch/dinner). Matches real household bookkeeping practice, not a theoretical model.
public class DailyMealEntry
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Count { get; set; }
    public string? Notes { get; set; }
    public DailyMealEntryStatus Status { get; set; } = DailyMealEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
