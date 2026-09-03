using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Expenses.DTOs;

public record ExpenseListQuery
{
    public string? CategoryId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public string? Merchant { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Tag { get; init; }
    public bool? IsRecurring { get; init; }
    public string? Search { get; init; }
    public ExpenseSortField SortBy { get; init; } = ExpenseSortField.Date;
    public bool SortDescending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
