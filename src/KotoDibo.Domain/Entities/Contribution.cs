using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// A direct cash deposit into the household's shared fund, independent of any specific purchase —
// distinct from BazarPurchase (money out for groceries), this is money in.
public class Contribution
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string ContributedByUserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
