using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.MealCalculation.DTOs;
using KotoDibo.Application.Features.MealCalculation.Interfaces;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.MealCalculation.Services;

public class MealCalculationService : IMealCalculationService
{
    private const int MaxRangeDays = 366;

    private readonly IRepository<BazarPurchase> _purchases;
    private readonly IRepository<Contribution> _contributions;
    private readonly IRepository<DailyMealEntry> _mealEntries;
    private readonly IHouseholdAccessService _access;

    public MealCalculationService(
        IRepository<BazarPurchase> purchases,
        IRepository<Contribution> contributions,
        IRepository<DailyMealEntry> mealEntries,
        IHouseholdAccessService access)
    {
        _purchases = purchases;
        _contributions = contributions;
        _mealEntries = mealEntries;
        _access = access;
    }

    public async Task<MealCalculationDto> GetMealRateAsync(string householdId, string callerUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewMealCalculation, cancellationToken);

        if (to < from)
        {
            throw FieldValidationException("to", "The end date must not be before the start date.");
        }

        if (to.DayNumber - from.DayNumber > MaxRangeDays)
        {
            throw FieldValidationException("to", $"The date range cannot exceed {MaxRangeDays} days.");
        }

        var purchases = await _purchases.FindAsync(
            p => p.HouseholdId == householdId && p.Status == FinancialEntryStatus.Active && p.Date >= from && p.Date <= to,
            cancellationToken);
        var foodCost = purchases.Sum(p => p.Amount);
        var purchasesByUser = purchases
            .GroupBy(p => p.PurchasedByUserId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var contributions = await _contributions.FindAsync(
            c => c.HouseholdId == householdId && c.Status == FinancialEntryStatus.Active && c.Date >= from && c.Date <= to,
            cancellationToken);
        var contributionsByUser = contributions
            .GroupBy(c => c.ContributedByUserId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        var mealEntries = await _mealEntries.FindAsync(
            e => e.HouseholdId == householdId && e.Status == DailyMealEntryStatus.Active && e.Date >= from && e.Date <= to,
            cancellationToken);
        var weightsByUser = mealEntries
            .GroupBy(e => e.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Count));

        var totalUnits = weightsByUser.Values.Sum();
        var mealCostByUser = totalUnits > 0
            ? MealCostAllocator.Allocate(foodCost, weightsByUser)
            : new Dictionary<string, decimal>();

        var userIds = weightsByUser.Keys
            .Union(purchasesByUser.Keys)
            .Union(contributionsByUser.Keys)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var members = userIds.Select(userId =>
        {
            var mealUnits = weightsByUser.GetValueOrDefault(userId, 0m);
            var mealCost = mealCostByUser.GetValueOrDefault(userId, 0m);

            // A Bazar purchase paid personally (BazarFundingSource.Personal) auto-generates a
            // mirrored Contribution row for the same amount (see BazarPurchaseService), so
            // contributionsByUser already reflects it — adding purchasesByUser here too would
            // double-count that spend. A purchase paid from the household fund intentionally
            // contributes nothing here: the buyer didn't personally give that money, the fund did.
            var contribution = contributionsByUser.GetValueOrDefault(userId, 0m);
            return new MealMemberCostDto
            {
                UserId = userId,
                MealUnits = mealUnits,
                MealCost = mealCost,
                BazarSpend = purchasesByUser.GetValueOrDefault(userId, 0m),
                Contribution = contribution,
                GiveTake = contribution - mealCost,
            };
        }).ToList();

        return new MealCalculationDto
        {
            From = from,
            To = to,
            FoodCost = foodCost,
            TotalMealUnits = totalUnits,
            MealRate = totalUnits > 0 ? foodCost / totalUnits : null,
            TotalContributions = members.Sum(m => m.Contribution),
            Members = members,
            CalculationVersion = "v2",
        };
    }

    private static KotoDibo.Application.Common.Exceptions.ValidationException FieldValidationException(string field, string message) => new(new Dictionary<string, string[]>
    {
        [field] = [message],
    });
}
