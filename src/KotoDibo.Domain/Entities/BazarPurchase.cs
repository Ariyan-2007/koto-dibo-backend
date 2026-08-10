using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

public class BazarPurchase
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string PurchasedByUserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Note { get; set; }
    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
