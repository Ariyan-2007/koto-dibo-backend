namespace KotoDibo.Application.Features.Expenses.DTOs;

public record ExpenseDto
{
    public string Id { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? Merchant { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public DateOnly Date { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? ReceiptUrl { get; init; }
    public string? RecurringExpenseId { get; init; }
    public bool IsRecurringGenerated { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
