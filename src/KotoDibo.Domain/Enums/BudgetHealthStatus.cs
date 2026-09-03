namespace KotoDibo.Domain.Enums;

// Whole-budget health indicator (distinct vocabulary from BudgetCategoryStatus, matching how
// product copy usually separates "how is this one envelope doing" from "how is my month going
// overall"). Thresholds live in BudgetThresholds so they stay centralized and tunable.
public enum BudgetHealthStatus
{
    NoBudget,
    Healthy,
    Warning,
    Overspending,
    Critical,
}
