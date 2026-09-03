namespace KotoDibo.Application.Features.RecurringExpenses.DTOs;

public record UpdateRecurringExpenseRequest
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? CategoryId { get; init; }
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? PaymentMethod { get; init; }
    public List<string>? Tags { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool? IsActive { get; init; }
}
