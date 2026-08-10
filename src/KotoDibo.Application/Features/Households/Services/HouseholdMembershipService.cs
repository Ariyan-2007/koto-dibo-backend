using FluentValidation;
using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Households.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Exceptions;

namespace KotoDibo.Application.Features.Households.Services;

public class HouseholdMembershipService : IHouseholdMembershipService
{
    private readonly IRepository<Household> _households;
    private readonly IRepository<HouseholdMembership> _memberships;
    private readonly IRepository<User> _users;
    private readonly IHouseholdAccessService _access;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<AddMemberRequest> _addMemberValidator;
    private readonly IValidator<UpdateMemberRoleRequest> _updateRoleValidator;

    public HouseholdMembershipService(
        IRepository<Household> households,
        IRepository<HouseholdMembership> memberships,
        IRepository<User> users,
        IHouseholdAccessService access,
        IDateTimeProvider dateTimeProvider,
        IValidator<AddMemberRequest> addMemberValidator,
        IValidator<UpdateMemberRoleRequest> updateRoleValidator)
    {
        _households = households;
        _memberships = memberships;
        _users = users;
        _access = access;
        _dateTimeProvider = dateTimeProvider;
        _addMemberValidator = addMemberValidator;
        _updateRoleValidator = updateRoleValidator;
    }

    public async Task<IReadOnlyList<HouseholdMemberDto>> GetMembersAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.ViewMembers, cancellationToken);

        var memberships = await _memberships.FindAsync(
            m => m.HouseholdId == householdId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);

        var userIds = memberships.Select(m => m.UserId).ToHashSet();
        var users = await _users.FindAsync(u => userIds.Contains(u.Id), cancellationToken);
        var usersById = users.ToDictionary(u => u.Id);

        return memberships
            .Where(m => usersById.ContainsKey(m.UserId))
            .OrderBy(m => m.JoinedAt)
            .Select(m => ToDto(m, usersById[m.UserId]))
            .ToList();
    }

    public async Task<HouseholdMemberDto> AddMemberAsync(string householdId, string callerUserId, AddMemberRequest request, CancellationToken cancellationToken = default)
    {
        await _addMemberValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.AddMember, cancellationToken);

        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);
        RequireActive(household);

        var newRole = Enum.Parse<HouseholdRole>(request.Role, ignoreCase: true);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var targetUser = await _users.FindOneAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken)
            ?? throw EmailValidationException("No account found with this email.");

        var existingActive = await _memberships.FindOneAsync(
            m => m.HouseholdId == householdId && m.UserId == targetUser.Id && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);
        if (existingActive is not null)
        {
            throw EmailValidationException("This user is already a member of this household.");
        }

        var now = _dateTimeProvider.UtcNow;
        var membership = new HouseholdMembership
        {
            HouseholdId = householdId,
            UserId = targetUser.Id,
            Role = newRole,
            Status = HouseholdMembershipStatus.Active,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _memberships.AddAsync(membership, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Closes the race between the pre-check above and this insert under concurrent adds.
            throw EmailValidationException("This user is already a member of this household.");
        }

        return ToDto(membership, targetUser);
    }

    public async Task RemoveMemberAsync(string householdId, string callerUserId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var callerMembership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.RemoveMember, cancellationToken);

        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);
        RequireActive(household);

        var targetMembership = await _memberships.FindOneAsync(
            m => m.HouseholdId == householdId && m.UserId == targetUserId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken) ?? throw new NotFoundException("HouseholdMembership", targetUserId);

        if (targetMembership.Role == HouseholdRole.Owner)
        {
            throw new DomainException("The household owner cannot be removed. Transfer ownership first.");
        }

        if (targetMembership.Role == HouseholdRole.Manager && callerMembership.Role != HouseholdRole.Owner)
        {
            throw new ForbiddenException("Only the household owner can remove a manager.");
        }

        var now = _dateTimeProvider.UtcNow;
        targetMembership.Status = HouseholdMembershipStatus.Removed;
        targetMembership.RemovedAt = now;
        targetMembership.RemovedBy = callerUserId;
        targetMembership.UpdatedAt = now;
        await _memberships.UpdateAsync(targetMembership, cancellationToken);
    }

    public async Task<HouseholdMemberDto> UpdateMemberRoleAsync(string householdId, string callerUserId, string targetUserId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        await _updateRoleValidator.ValidateAndThrowAsync(request, cancellationToken);
        await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.UpdateMemberRole, cancellationToken);

        if (targetUserId == callerUserId)
        {
            throw new DomainException("You cannot change your own role. Leave or transfer ownership instead.");
        }

        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);
        RequireActive(household);

        var targetMembership = await _memberships.FindOneAsync(
            m => m.HouseholdId == householdId && m.UserId == targetUserId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken) ?? throw new NotFoundException("HouseholdMembership", targetUserId);

        if (targetMembership.Role == HouseholdRole.Owner)
        {
            throw new DomainException("Ownership must be transferred, not changed via a role update.");
        }

        targetMembership.Role = Enum.Parse<HouseholdRole>(request.Role, ignoreCase: true);
        targetMembership.UpdatedAt = _dateTimeProvider.UtcNow;
        await _memberships.UpdateAsync(targetMembership, cancellationToken);

        var targetUser = await _users.GetByIdAsync(targetUserId, cancellationToken)
            ?? throw new NotFoundException("User", targetUserId);
        return ToDto(targetMembership, targetUser);
    }

    public async Task LeaveAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default)
    {
        var callerMembership = await _access.RequireMembershipAsync(householdId, callerUserId, HouseholdPermission.LeaveHousehold, cancellationToken);
        var household = await _households.GetByIdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Household", householdId);

        var now = _dateTimeProvider.UtcNow;

        if (callerMembership.Role == HouseholdRole.Owner)
        {
            var otherActiveMembers = await _memberships.FindAsync(
                m => m.HouseholdId == householdId && m.Status == HouseholdMembershipStatus.Active && m.UserId != callerUserId,
                cancellationToken);

            if (otherActiveMembers.Count > 0)
            {
                throw new DomainException("Transfer ownership to another member before leaving this household.");
            }

            // The owner was the last active member: the household has no one left to run it.
            household.Status = HouseholdStatus.Archived;
            household.ArchivedAt = now;
            household.UpdatedAt = now;
            await _households.UpdateAsync(household, cancellationToken);
        }

        callerMembership.Status = HouseholdMembershipStatus.Left;
        callerMembership.LeftAt = now;
        callerMembership.UpdatedAt = now;
        await _memberships.UpdateAsync(callerMembership, cancellationToken);
    }

    private static void RequireActive(Household household)
    {
        if (household.Status != HouseholdStatus.Active)
        {
            throw new DomainException("This household is archived and no longer accepts membership changes.");
        }
    }

    private static KotoDibo.Application.Common.Exceptions.ValidationException EmailValidationException(string message) => new(new Dictionary<string, string[]>
    {
        [nameof(AddMemberRequest.Email)] = [message],
    });

    private static HouseholdMemberDto ToDto(HouseholdMembership membership, User user) => new()
    {
        UserId = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = membership.Role.ToString(),
        JoinedAt = membership.JoinedAt,
    };
}
