namespace KotoDibo.Application.Features.Budget.DTOs;

// Delta, not an absolute new amount — positive increases PlannedAmount ("additional allocation"),
// negative decreases it ("reduced allocation"); either way it's logged as a BudgetAdjustment so
// the category's history reads as a sequence of changes rather than one opaque overwrite.
public record AdjustBudgetCategoryRequest
{
    public decimal Delta { get; init; }
    public string? Reason { get; init; }
}
