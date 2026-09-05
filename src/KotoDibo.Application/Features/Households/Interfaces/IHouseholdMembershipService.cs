using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Interfaces;

public interface IHouseholdMembershipService
{
    Task<IReadOnlyList<HouseholdMemberDto>> GetMembersAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    Task<HouseholdMemberDto> AddMemberAsync(string householdId, string callerUserId, AddMemberRequest request, CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(string householdId, string callerUserId, string targetUserId, CancellationToken cancellationToken = default);

    Task<HouseholdMemberDto> UpdateMemberRoleAsync(string householdId, string callerUserId, string targetUserId, UpdateMemberRoleRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdMemberDto> TransferOwnershipAsync(string householdId, string callerUserId, TransferOwnershipRequest request, CancellationToken cancellationToken = default);

    Task LeaveAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);
}
