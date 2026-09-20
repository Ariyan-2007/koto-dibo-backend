namespace KotoDibo.Domain.Entities;

// One row per household, used purely as a write-conflict point: any operation that checks the fund
// balance and then spends from it (a Withdrawal, a HouseholdFund Bazar purchase) bumps this row
// inside its transaction first. Two such operations racing on the same household then conflict on
// this row, the loser's transaction is retried, and its balance check re-runs against the winner's
// committed write — so the fund can't be overdrawn by concurrent requests. Id is the HouseholdId.
public class HouseholdLedgerLock
{
    public string Id { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTime UpdatedAt { get; set; }
}
