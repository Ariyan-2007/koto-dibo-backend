namespace KotoDibo.Domain.Enums;

// Where the cash for a Bazar purchase actually came from. This is what decides whether the
// purchase mirrors itself as a Contribution (Personal) or draws down the shared fund instead
// (HouseholdFund) — see BazarPurchaseService.CreateAsync and HouseholdBalanceCalculator.
public enum BazarFundingSource
{
    Personal,
    HouseholdFund,
}
