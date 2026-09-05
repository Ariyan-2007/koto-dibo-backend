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
        // Every active Bazar expense that actually reduces the balance: drawn directly from the
        // fund, or a personal-pocket purchase offsetting its own mirrored Contribution (see
        // HouseholdBalanceCalculator, the single source of truth this mirrors so the two never
        // drift apart).
        var totalSpentFromFund = purchases
            .Where(p => p.FundingSource == BazarFundingSource.HouseholdFund || p.LinkedContributionId is not null)
            .Sum(p => p.Amount);
        var currency = contributions.Select(c => c.Currency).FirstOrDefault()
            ?? purchases.Select(p => p.Currency).FirstOrDefault()
            ?? string.Empty;

        return new HouseholdBalanceDto
        {
            HouseholdId = householdId,
            TotalContributions = totalContributions,
            TotalSpentFromFund = totalSpentFromFund,
            CurrentBalance = HouseholdBalanceCalculator.Calculate(contributions, purchases),
            Currency = currency,
            AsOf = _dateTimeProvider.UtcNow,
        };
    }

    public async Task<IReadOnlyList<HouseholdLedgerTransactionDto>> GetTransactionsAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewHouseholdBalance, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var parsedStatus = status is null ? (FinancialEntryStatus?)null : Enum.Parse<FinancialEntryStatus>(status, ignoreCase: true);

        var contributions = await _contributions.FindAsync(
            c => c.HouseholdId == householdId && c.Date >= effectiveFrom && c.Date <= effectiveTo,
            cancellationToken);
        var purchases = await _purchases.FindAsync(
            p => p.HouseholdId == householdId && p.Date >= effectiveFrom && p.Date <= effectiveTo,
            cancellationToken);

        IEnumerable<Contribution> filteredContributions = contributions;
        IEnumerable<BazarPurchase> filteredPurchases = purchases;
        if (parsedStatus is { } s)
        {
            filteredContributions = filteredContributions.Where(c => c.Status == s);
            filteredPurchases = filteredPurchases.Where(p => p.Status == s);
        }

        var contributionEntries = filteredContributions.Select(c => new HouseholdLedgerTransactionDto
        {
            Id = c.Id,
            HouseholdId = c.HouseholdId,
            EntryType = "Contribution",
            Direction = "In",
            BalanceImpact = c.Status == FinancialEntryStatus.Active ? c.Amount : 0m,
            Date = c.Date,
            Amount = c.Amount,
            Currency = c.Currency,
            UserId = c.ContributedByUserId,
            CreatedByUserId = c.CreatedByUserId,
            SourceType = c.SourceType.ToString(),
            LinkedEntryId = c.SourceBazarPurchaseId,
            Note = c.Notes,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
        });

        var purchaseEntries = filteredPurchases.Select(p => new HouseholdLedgerTransactionDto
        {
            Id = p.Id,
            HouseholdId = p.HouseholdId,
            EntryType = "BazarPurchase",
            Direction = "Out",
            // Mirrors HouseholdBalanceCalculator exactly: a HouseholdFund purchase draws the pool
            // down directly; a Personal purchase with a mirrored Contribution (LinkedEntryId set)
            // also carries its own -Amount here, so that row and its mirrored Contribution's own
            // +Amount row (above) sum to zero together — net-zero balance impact, not double-counted.
            BalanceImpact = p.Status == FinancialEntryStatus.Active && (p.FundingSource == BazarFundingSource.HouseholdFund || p.LinkedContributionId is not null) ? -p.Amount : 0m,
            Date = p.Date,
            Amount = p.Amount,
            Currency = p.Currency,
            UserId = p.PurchasedByUserId,
            CreatedByUserId = p.CreatedByUserId,
            SourceType = p.FundingSource.ToString(),
            LinkedEntryId = p.LinkedContributionId,
            Note = p.Note,
            Status = p.Status.ToString(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        });

        return contributionEntries.Concat(purchaseEntries)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
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

    public async Task<string?> GetEstablishedCurrencyAsync(string householdId, CancellationToken cancellationToken = default)
    {
        var contribution = await _contributions.FindOneAsync(
            c => c.HouseholdId == householdId && c.Status == FinancialEntryStatus.Active,
            cancellationToken);
        if (contribution is not null)
        {
            return contribution.Currency;
        }

        var purchase = await _purchases.FindOneAsync(
            p => p.HouseholdId == householdId && p.Status == FinancialEntryStatus.Active,
            cancellationToken);
        return purchase?.Currency;
    }
}
