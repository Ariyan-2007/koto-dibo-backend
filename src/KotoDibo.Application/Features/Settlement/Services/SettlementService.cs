using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.BillSplit.Interfaces;
using KotoDibo.Application.Features.MealCalculation.Interfaces;
using KotoDibo.Application.Features.Settlement.DTOs;
using KotoDibo.Application.Features.Settlement.Interfaces;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Settlement.Services;

// Thin aggregation layer — this is deliberately additive: it never recomputes anything either
// MealCalculationService or BillSplitService already produce, it just sums their outputs for a
// period so a member sees one net number instead of reconciling two screens mentally.
public class SettlementService : ISettlementService
{
    private readonly IMealCalculationService _mealCalculationService;
    private readonly IBillSplitService _billSplitService;
    private readonly IHouseholdAccessService _access;

    public SettlementService(
        IMealCalculationService mealCalculationService,
        IBillSplitService billSplitService,
        IHouseholdAccessService access)
    {
        _mealCalculationService = mealCalculationService;
        _billSplitService = billSplitService;
        _access = access;
    }

    public async Task<HouseholdSettlementDto> GetSettlementAsync(string householdId, string callerUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewSettlement, cancellationToken);

        var mealResult = await _mealCalculationService.GetMealRateAsync(householdId, callerUserId, from, to, cancellationToken);
        var mealGiveTakeByUser = mealResult.Members.ToDictionary(m => m.UserId, m => m.GiveTake);

        var billSplits = await _billSplitService.GetListAsync(householdId, callerUserId, from, to, status: nameof(FinancialEntryStatus.Active), cancellationToken);

        var billSplitOwedByUser = new Dictionary<string, decimal>();
        foreach (var billSplit in billSplits)
        {
            var settlement = await _billSplitService.GetSettlementAsync(householdId, callerUserId, billSplit.Id, cancellationToken);
            foreach (var member in settlement.Members)
            {
                billSplitOwedByUser[member.UserId] = billSplitOwedByUser.GetValueOrDefault(member.UserId, 0m) + member.TotalOwed;
            }
        }

        var userIds = mealGiveTakeByUser.Keys
            .Union(billSplitOwedByUser.Keys)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var members = userIds.Select(userId =>
        {
            var mealGiveTake = mealGiveTakeByUser.GetValueOrDefault(userId, 0m);
            var billSplitOwed = billSplitOwedByUser.GetValueOrDefault(userId, 0m);
            return new HouseholdMemberSettlementDto
            {
                UserId = userId,
                MealGiveTake = mealGiveTake,
                BillSplitOwed = billSplitOwed,
                NetBalance = mealGiveTake - billSplitOwed,
            };
        }).ToList();

        return new HouseholdSettlementDto
        {
            HouseholdId = householdId,
            From = from,
            To = to,
            TotalMealGiveTake = members.Sum(m => m.MealGiveTake),
            TotalBillSplitOwed = members.Sum(m => m.BillSplitOwed),
            Members = members,
        };
    }
}
