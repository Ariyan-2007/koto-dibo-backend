namespace KotoDibo.Application.Features.Budget.DTOs;

public record TransferBudgetCategoryRequest
{
    public string ToCategoryAllocationId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
}
