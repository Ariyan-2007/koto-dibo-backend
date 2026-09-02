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

    // Personal (default): the buyer paid out of pocket — CreateAsync mirrors this amount as an
    // auto-generated Contribution so it counts as money the household received. HouseholdFund:
    // paid using the shared balance instead — no mirrored Contribution, and this amount is
    // subtracted from the balance (see HouseholdBalanceCalculator).
    public BazarFundingSource FundingSource { get; set; } = BazarFundingSource.Personal;

    // Set only when FundingSource is Personal and Amount is positive: the id of the Contribution
    // auto-generated to mirror this purchase. Kept in sync by BazarPurchaseService whenever this
    // purchase is updated or cancelled.
    public string? LinkedContributionId { get; set; }

    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
