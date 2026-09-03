namespace KotoDibo.Domain.Entities;

// One category's "envelope" within a Budget period. PlannedAmount is the current planned figure
// (every change to it is logged as a BudgetAdjustment, so it can move without losing history);
// RolloverAmount is a separate additive bucket carried forward from the prior period's leftover
// (or deficit) via Budget.Rollover, kept distinct so the API can always show base vs. rollover
// vs. total available rather than collapsing them into one opaque number.
public class BudgetCategoryAllocation
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public bool RolloverEnabled { get; set; }
    public decimal RolloverAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
