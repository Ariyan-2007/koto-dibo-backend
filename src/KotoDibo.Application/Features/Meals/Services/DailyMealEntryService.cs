using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Meals.DTOs;
using KotoDibo.Application.Features.Meals.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Policies;

namespace KotoDibo.Application.Features.Meals.Services;

public class DailyMealEntryService : IDailyMealEntryService
{
    private readonly IRepository<DailyMealEntry> _entries;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<SetMealCountRequest> _setValidator;

    public DailyMealEntryService(
        IRepository<DailyMealEntry> entries,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<SetMealCountRequest> setValidator)
    {
        _entries = entries;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _setValidator = setValidator;
    }

    public async Task<DailyMealEntryDto> SetCountAsync(string householdId, string callerUserId, string targetUserId, DateOnly date, SetMealCountRequest request, CancellationToken cancellationToken = default)
    {
        await _setValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.RecordOwnMealCount, cancellationToken);
        RequireTargetAccess(membership.Role, callerUserId, targetUserId);
        RequireNotFuture(date);

        // Confirms the target is an active member of this household (everyone has ViewHousehold).
        await _access.RequireMembershipAsync(householdId, targetUserId, HouseholdPermission.ViewHousehold, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var existing = await _entries.FindOneAsync(
            e => e.HouseholdId == householdId && e.UserId == targetUserId && e.Date == date && e.Status == DailyMealEntryStatus.Active,
            cancellationToken);

        if (existing is not null)
        {
            existing.Count = request.Count;
            existing.Notes = request.Notes?.Trim();
            existing.UpdatedAt = now;
            await _entries.UpdateAsync(existing, cancellationToken);
            return ToDto(existing);
        }

        var entry = new DailyMealEntry
        {
            HouseholdId = householdId,
            UserId = targetUserId,
            Date = date,
            Count = request.Count,
            Notes = request.Notes?.Trim(),
            Status = DailyMealEntryStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _entries.AddAsync(entry, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            var winner = await _entries.FindOneAsync(
                e => e.HouseholdId == householdId && e.UserId == targetUserId && e.Date == date && e.Status == DailyMealEntryStatus.Active,
                cancellationToken);
            if (winner is not null)
            {
                winner.Count = request.Count;
                winner.Notes = request.Notes?.Trim();
                winner.UpdatedAt = now;
                await _entries.UpdateAsync(winner, cancellationToken);
                return ToDto(winner);
            }

            throw;
        }

        return ToDto(entry);
    }

    public async Task RemoveAsync(string householdId, string callerUserId, string targetUserId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.RecordOwnMealCount, cancellationToken);
        RequireTargetAccess(membership.Role, callerUserId, targetUserId);

        var existing = await _entries.FindOneAsync(
            e => e.HouseholdId == householdId && e.UserId == targetUserId && e.Date == date && e.Status == DailyMealEntryStatus.Active,
            cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Status = DailyMealEntryStatus.Removed;
        existing.UpdatedAt = _dateTimeProvider.UtcNow;
        await _entries.UpdateAsync(existing, cancellationToken);
    }

    public async Task<IReadOnlyList<DailyMealEntryDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? userId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewMeals, cancellationToken);

        var effectiveFrom = from ?? DateOnly.MinValue;
        var effectiveTo = to ?? DateOnly.MaxValue;
        var entries = await _entries.FindAsync(
            e => e.HouseholdId == householdId && e.Status == DailyMealEntryStatus.Active && e.Date >= effectiveFrom && e.Date <= effectiveTo,
            cancellationToken);

        IEnumerable<DailyMealEntry> filtered = entries;
        if (userId is not null)
        {
            filtered = filtered.Where(e => e.UserId == userId);
        }

        return filtered
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.UserId, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList();
    }

    private static void RequireTargetAccess(HouseholdRole callerRole, string callerUserId, string targetUserId)
    {
        if (targetUserId == callerUserId)
        {
            return;
        }

        if (!HouseholdRolePolicy.HasPermission(callerRole, HouseholdPermission.RecordAnyMealCount))
        {
            throw new ForbiddenException("You do not have permission to record meal counts for other members.");
        }
    }

    private void RequireNotFuture(DateOnly date)
    {
        var today = DateOnly.FromDateTime(_dateTimeProvider.UtcNow);
        if (date > today)
        {
            throw new KotoDibo.Application.Common.Exceptions.ValidationException(new Dictionary<string, string[]>
            {
                ["date"] = ["Date cannot be in the future."],
            });
        }
    }

    private static DailyMealEntryDto ToDto(DailyMealEntry entry) => new()
    {
        Id = entry.Id,
        HouseholdId = entry.HouseholdId,
        UserId = entry.UserId,
        Date = entry.Date,
        Count = entry.Count,
        Notes = entry.Notes,
        Status = entry.Status.ToString(),
        CreatedAt = entry.CreatedAt,
        UpdatedAt = entry.UpdatedAt,
    };
}
