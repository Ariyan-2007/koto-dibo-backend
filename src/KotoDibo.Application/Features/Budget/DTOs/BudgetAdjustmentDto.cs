namespace KotoDibo.Application.Features.Budget.DTOs;

public record BudgetAdjustmentDto
{
    public string Id { get; init; } = string.Empty;
    public string BudgetCategoryAllocationId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public string? RelatedCategoryAllocationId { get; init; }
    public string? Reason { get; init; }
    public DateTime CreatedAt { get; init; }
}
