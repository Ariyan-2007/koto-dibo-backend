namespace KotoDibo.Domain.Entities;

// Cash a member takes back out of the household's shared fund — the mirror image of Contribution.
// It draws the pool down (see HouseholdBalanceCalculator) and is deducted from the withdrawing
// member's own contribution total in the meal calculation, so they can't withdraw money that was
// never theirs without it showing up in their give/take. Hard-deleted like Bazar/Contribution, so
// there is no Status: every stored row is live.
public class Withdrawal
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string WithdrawnByUserId { get; set; } = string.Empty;

    // Who actually submitted the record — equals WithdrawnByUserId unless an Owner/Manager
    // recorded it on the member's behalf.
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
