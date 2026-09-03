using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// One period's budget envelope (e.g. "January 2026"). Deliberately period-scoped rather than
// "the" ongoing budget — a household/person's allocations legitimately differ month to month, so
// each period gets its own Budget row with its own BudgetCategoryAllocation children rather than
// one mutable budget whose category amounts get silently overwritten every period.
public class Budget
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Currency { get; set; } = string.Empty;
    public BudgetPeriodType PeriodType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
