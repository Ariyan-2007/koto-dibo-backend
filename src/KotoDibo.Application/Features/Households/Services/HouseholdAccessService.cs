using KotoDibo.Application.Common.Exceptions;
using KotoDibo.Application.Common.Interfaces;
using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using KotoDibo.Domain.Policies;

namespace KotoDibo.Application.Features.Households.Services;

public class HouseholdAccessService : IHouseholdAccessService
{
    private readonly IRepository<HouseholdMembership> _memberships;

    public HouseholdAccessService(IRepository<HouseholdMembership> memberships)
    {
        _memberships = memberships;
    }

    public async Task<HouseholdMembership> RequireMembershipAsync(string householdId, string userId, HouseholdPermission permission, CancellationToken cancellationToken = default)
    {
        var membership = await _memberships.FindOneAsync(
            m => m.HouseholdId == householdId && m.UserId == userId && m.Status == HouseholdMembershipStatus.Active,
            cancellationToken);

        // Same outcome whether the household doesn't exist or the caller simply isn't a member of
        // it — telling those apart would let a caller enumerate household IDs by probing.
        if (membership is null)
        {
            throw new NotFoundException("Household", householdId);
        }

        if (!HouseholdRolePolicy.HasPermission(membership.Role, permission))
        {
            throw new ForbiddenException("You do not have permission to perform this action in this household.");
        }

        return membership;
    }
}
