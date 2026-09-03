namespace KotoDibo.Domain.Enums;

// Per-category budget-vs-actual health, computed live by BudgetCalculator — never stored, so it's
// always derived from the current set of expenses rather than going stale.
public enum BudgetCategoryStatus
{
    // No amount was ever allocated to this category for the period (TotalAvailable == 0).
    NoBudget,
    OnTrack,
    Warning,
    Overspent,
}
