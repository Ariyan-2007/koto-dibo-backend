using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Households.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Households.Services;

public class HouseholdService : IHouseholdService
{
    private readonly IRepository<Household> _households;
    private readonly IRepository<HouseholdMembership> _memberships;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateHouseholdRequest> _createValidator;
    private readonly IValidator<UpdateHouseholdRequest> _updateValidator;

    public HouseholdService(
        IRepository<Household> households,
        IRepository<HouseholdMembership> memberships,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateHouseholdRequest> createValidator,
        IValidator<UpdateHouseholdRequest> updateValidator)
    {
        _households = households;
        _memberships = memberships;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<HouseholdDto> CreateAsync(string ownerUserId, CreateHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var household = new Household
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Type = request.Type?.Trim(),
            Status = HouseholdStatus.Active,
            OwnerUserId = ownerUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _households.AddAsync(household, cancellationToken);

        var membership = new HouseholdMembership
        {
            HouseholdId = household.Id,
            UserId = ownerUserId,
            Role = HouseholdRole.Owner,
            Status = HouseholdMembershipStatus.Active,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _memberships.AddAsync(membership, cancellationToken);
        }
        catch
        {
            // Compensating action: a household with no owner membership is unusable and would be
            // invisible to GetMyHouseholdsAsync anyway. No cross-collection Mongo transaction here,
            // same reasoning as Auth registration's User/UserCredential pair.
            await _households.DeleteAsync(household.Id, CancellationToken.None);
            throw;
        }

        return ToDto(household, membership.Role, memberCount: 1);
    }

    public async Task<HouseholdDto> GetByIdAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewHousehold, cancellationToken);
        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);

        var memberCount = await CountActiveMembersAsync(householdId, cancellationToken);
        return ToDto(household, membership.Role, memberCount);
    }

    public async Task<IReadOnlyList<HouseholdDto>> GetMyHouseholdsAsync(string callerUserId, CancellationToken cancellationToken = default)
    {
        var myMemberships = await _memberships.FindAsync(
            m => m.UserId == callerUserId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);

        if (myMemberships.Count == 0)
        {
            return [];
        }

        var householdIds = myMemberships.Select(m => m.HouseholdId).ToHashSet();
        var households = await _households.FindAsync(h => householdIds.Contains(h.Id), cancellationToken);

        var activeMemberships = await _memberships.FindAsync(
            m => householdIds.Contains(m.HouseholdId) && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);
        var memberCounts = activeMemberships
            .GroupBy(m => m.HouseholdId)
            .ToDictionary(g => g.Key, g => g.Count());
        var callerRoleByHousehold = myMemberships.ToDictionary(m => m.HouseholdId, m => m.Role);

        return households
            .Select(h => ToDto(h, callerRoleByHousehold[h.Id], memberCounts.GetValueOrDefault(h.Id)))
            .OrderByDescending(h => h.UpdatedAt)
            .ToList();
    }

    public async Task<HouseholdDto> UpdateAsync(string householdId, string callerUserId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.UpdateHousehold, cancellationToken);
        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);

        RequireActive(household);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            household.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            household.Description = request.Description.Trim();
        }

        if (request.Type is not null)
        {
            household.Type = request.Type.Trim();
        }

        household.UpdatedAt = _dateTimeProvider.UtcNow;
        await _households.UpdateAsync(household, cancellationToken);

        var memberCount = await CountActiveMembersAsync(householdId, cancellationToken);
        return ToDto(household, membership.Role, memberCount);
    }

    public async Task<HouseholdDto> ArchiveAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ArchiveHousehold, cancellationToken);
        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);

        RequireActive(household);

        var now = _dateTimeProvider.UtcNow;
        household.Status = HouseholdStatus.Archived;
        household.ArchivedAt = now;
        household.UpdatedAt = now;
        await _households.UpdateAsync(household, cancellationToken);

        var memberCount = await CountActiveMembersAsync(householdId, cancellationToken);
        return ToDto(household, membership.Role, memberCount);
    }

    public async Task<HouseholdDto> RestoreAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        var membership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.RestoreHousehold, cancellationToken);
        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);

        if (household.Status != HouseholdStatus.Archived)
        {
            throw new DomainException("Only an archived household can be restored.");
        }

        var now = _dateTimeProvider.UtcNow;
        household.Status = HouseholdStatus.Active;
        household.ArchivedAt = null;
        household.UpdatedAt = now;
        await _households.UpdateAsync(household, cancellationToken);

        var memberCount = await CountActiveMembersAsync(householdId, cancellationToken);
        return ToDto(household, membership.Role, memberCount);
    }

    private async Task<int> CountActiveMembersAsync(string householdId, CancellationToken cancellationToken)
    {
        var members = await _memberships.FindAsync(
            m => m.HouseholdId == householdId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);
        return members.Count;
    }

    private static void RequireActive(Household household)
    {
        if (household.Status != HouseholdStatus.Active)
        {
            throw new DomainException("This household is archived and no longer accepts changes.");
        }
    }

    private static HouseholdDto ToDto(Household household, HouseholdRole callerRole, int memberCount) => new()
    {
        Id = household.Id,
        Name = household.Name,
        Description = household.Description,
        Type = household.Type,
        Status = household.Status.ToString(),
        OwnerUserId = household.OwnerUserId,
        MemberCount = memberCount,
        CallerRole = callerRole.ToString(),
        CreatedAt = household.CreatedAt,
        UpdatedAt = household.UpdatedAt,
        ArchivedAt = household.ArchivedAt,
    };
}
