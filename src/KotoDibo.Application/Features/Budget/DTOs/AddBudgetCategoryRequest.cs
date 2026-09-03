namespace KotoDibo.Application.Features.Budget.DTOs;

public record AddBudgetCategoryRequest
{
    public string CategoryId { get; init; } = string.Empty;
    public decimal PlannedAmount { get; init; }
    public bool RolloverEnabled { get; init; }
    public string? Notes { get; init; }
}
