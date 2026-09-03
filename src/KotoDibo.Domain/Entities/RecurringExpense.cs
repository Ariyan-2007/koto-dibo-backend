using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// A template that RecurringExpenseGenerator turns into concrete Expense rows. NextOccurrenceDate
// is the next date generation is due; LastGeneratedDate is the most recent occurrence actually
// materialized — generation only ever walks forward from LastGeneratedDate (or StartDate if
// nothing has been generated yet), which is what makes repeated generation runs idempotent.
public class RecurringExpense
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    public string? Merchant { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Cash;
    public List<string> Tags { get; set; } = [];

    public RecurrenceFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextOccurrenceDate { get; set; }
    public DateOnly? LastGeneratedDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
