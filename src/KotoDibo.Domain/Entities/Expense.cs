using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

public class Expense
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    // Snapshotted at write time so a later category rename/deactivation never rewrites history —
    // the expense keeps reading exactly the name it was tagged with, per the "historical accuracy"
    // requirement (see EXPENSE_MODULE_PROMPT §35).
    public string CategoryName { get; set; } = string.Empty;

    public string? Merchant { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateOnly Date { get; set; }
    public ExpensePaymentMethod PaymentMethod { get; set; } = ExpensePaymentMethod.Cash;
    public List<string> Tags { get; set; } = [];
    public string? ReceiptUrl { get; set; }

    // Set when this row was produced by RecurringExpenseGenerator rather than entered by hand.
    public string? RecurringExpenseId { get; set; }

    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
