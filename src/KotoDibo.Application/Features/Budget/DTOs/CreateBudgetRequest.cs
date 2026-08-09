namespace KotoDibo.Application.Features.Budget.DTOs;

public record CreateBudgetRequest
{
    public string Period { get; init; } = string.Empty;
    public decimal Amount { get; init; } = default;
}
