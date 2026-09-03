namespace KotoDibo.Application.Features.RecurringExpenses.DTOs;

public record RecurringExpenseDto
{
    public string Id { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string Frequency { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateOnly NextOccurrenceDate { get; init; }
    public DateOnly? LastGeneratedDate { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
