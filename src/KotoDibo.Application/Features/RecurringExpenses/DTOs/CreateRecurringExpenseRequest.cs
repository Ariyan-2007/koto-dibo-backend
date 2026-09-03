namespace KotoDibo.Application.Features.RecurringExpenses.DTOs;

public record CreateRecurringExpenseRequest
{
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string CategoryId { get; init; } = string.Empty;
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? PaymentMethod { get; init; }
    public List<string>? Tags { get; init; }
    public string Frequency { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}
