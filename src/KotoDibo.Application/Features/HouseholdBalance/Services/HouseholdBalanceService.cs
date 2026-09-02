using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.HouseholdBalance.DTOs;
using KotoDibo.Application.Features.HouseholdBalance.Interfaces;
using KotoDibo.Domain.Calculations;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.HouseholdBalance.Services;

public class HouseholdBalanceService : IHouseholdBalanceService
{
    private readonly IRepository<Contribution> _contributions;
    private readonly IRepository<BazarPurchase> _purchases;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;

    public HouseholdBalanceService(
        IRepository<Contribution> contributions,
        IRepository<BazarPurchase> purchases,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider)
    {
        _contributions = contributions;
        _purchases = purchases;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<HouseholdBalanceDto> GetBalanceAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewHouseholdBalance, cancellationToken);

        var contributions = await _contributions.FindAsync(
            c => c.HouseholdId == householdId && c.Status == FinancialEntryStatus.Active,
            cancellationToken);
        var purchases = await _purchases.FindAsync(
            p => p.HouseholdId == householdId && p.Status == FinancialEntryStatus.Active,
            cancellationToken);

        var totalContributions = contributions.Sum(c => c.Amount);
        var totalSpentFromFund = purchases
            .Where(p => p.FundingSource == BazarFundingSource.HouseholdFund)
            .Sum(p => p.Amount);
        var currency = contributions.Select(c => c.Currency).FirstOrDefault()
            ?? purchases.Select(p => p.Currency).FirstOrDefault()
            ?? string.Empty;

        return new HouseholdBalanceDto
        {
            HouseholdId = householdId,
            TotalContributions = totalContributions,
            TotalSpentFromFund = totalSpentFromFund,
            CurrentBalance = totalContributions - totalSpentFromFund,
            Currency = currency,
            AsOf = _dateTimeProvider.UtcNow,
        };
    }

    public async Task<decimal> GetCurrentBalanceAsync(string householdId, CancellationToken cancellationToken = default)
    {
        var contributions = await _contributions.FindAsync(
            c => c.HouseholdId == householdId && c.Status == FinancialEntryStatus.Active,
            cancellationToken);
        var purchases = await _purchases.FindAsync(
            p => p.HouseholdId == householdId && p.Status == FinancialEntryStatus.Active,
            cancellationToken);

        return HouseholdBalanceCalculator.Calculate(contributions, purchases);
    }
}
