namespace KotoDibo.Application.Features.Budget.DTOs;

public record BudgetDto
{
    public string Id { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public decimal Amount { get; init; } = default;
}
