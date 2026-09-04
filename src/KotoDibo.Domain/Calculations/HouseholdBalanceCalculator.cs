using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Calculations;

// The household's shared fund is real cash members have put in, minus what has actually been
// drawn out of that pool to pay for groceries — money-in transactions minus money-out
// transactions, with nothing double-counted.
//
// A Bazar purchase paid FROM the fund (BazarFundingSource.HouseholdFund) draws it down directly.
// A Bazar purchase paid personally (BazarFundingSource.Personal, Amount > 0) mirrors itself as a
// Contribution for the same amount (see BazarPurchaseService.CreateAsync) — that mirror is a real
// money-in transaction, so it must be paired with an equal money-out subtraction here, or the
// balance would grow by the purchase amount even though no cash actually entered the pool (the
// buyer paid the store directly, not the household). LinkedContributionId being set is exactly
// how a purchase signals "I have a mirrored Contribution to offset" — checking FundingSource alone
// would miss this. A negative "leftover" correction entry (Amount <= 0) has no mirror and is
// excluded from both sides, exactly as before.
public static class HouseholdBalanceCalculator
{
    public static decimal Calculate(IEnumerable<Contribution> activeContributions, IEnumerable<BazarPurchase> activeBazarPurchases)
    {
        var totalContributions = activeContributions.Sum(c => c.Amount);
        var totalBazarExpense = activeBazarPurchases
            .Where(p => p.FundingSource == BazarFundingSource.HouseholdFund || p.LinkedContributionId is not null)
            .Sum(p => p.Amount);

        return totalContributions - totalBazarExpense;
    }
}
