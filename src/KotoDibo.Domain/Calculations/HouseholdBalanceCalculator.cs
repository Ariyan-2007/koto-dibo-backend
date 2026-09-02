using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Calculations;

// The household's shared fund is real cash members have put in, minus what has actually been
// drawn out of that pool to pay for groceries. A Bazar purchase paid personally
// (BazarFundingSource.Personal) never touches this balance either way: it mirrors itself as a
// Contribution (see BazarPurchaseService.CreateAsync), and that mirrored row is what actually
// moves the balance. Only a purchase explicitly paid FROM the fund (BazarFundingSource.
// HouseholdFund) draws it down. So this is deliberately just one subtraction — the "personal
// spend cancels out its own mirrored contribution" behavior falls out of that on its own, it
// isn't special-cased here.
public static class HouseholdBalanceCalculator
{
    public static decimal Calculate(IEnumerable<Contribution> activeContributions, IEnumerable<BazarPurchase> activeBazarPurchases)
    {
        var totalContributions = activeContributions.Sum(c => c.Amount);
        var totalSpentFromFund = activeBazarPurchases
            .Where(p => p.FundingSource == BazarFundingSource.HouseholdFund)
            .Sum(p => p.Amount);

        return totalContributions - totalSpentFromFund;
    }
}
