using KotoDibo.Application.Features.Invites.DTOs;

namespace KotoDibo.Application.Features.Invites.Interfaces;

public interface IHouseholdInviteService
{
    Task<HouseholdInviteDto> CreateAsync(string householdId, string callerUserId, CreateHouseholdInviteRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseholdInviteDto>> GetPendingAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    Task RevokeAsync(string householdId, string callerUserId, string inviteId, CancellationToken cancellationToken = default);

    // No household/RBAC scoping — the Code itself is the credential, and the caller isn't a member
    // of the household yet by definition.
    Task<InvitePreviewDto> PreviewAsync(string code, string callerUserId, CancellationToken cancellationToken = default);

    Task<AcceptInviteResultDto> AcceptAsync(string code, string callerUserId, CancellationToken cancellationToken = default);
}
