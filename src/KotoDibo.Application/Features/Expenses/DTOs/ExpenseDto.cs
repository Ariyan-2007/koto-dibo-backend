namespace KotoDibo.Application.Features.Expenses.DTOs;

public record ExpenseDto
{
    public string Id { get; init; } = string.Empty;
    public decimal Amount { get; init; } = default;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly Date { get; init; } = default;
}
