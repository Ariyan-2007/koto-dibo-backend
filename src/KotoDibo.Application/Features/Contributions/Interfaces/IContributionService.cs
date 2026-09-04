using KotoDibo.Application.Features.Contributions.DTOs;

namespace KotoDibo.Application.Features.Contributions.Interfaces;

public interface IContributionService
{
    Task<ContributionDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateContributionRequest request, CancellationToken cancellationToken = default);

    Task<ContributionDto> GetByIdAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContributionDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default);

    Task<ContributionDto> UpdateAsync(string householdId, string callerUserId, string contributionId, UpdateContributionRequest request, CancellationToken cancellationToken = default);

    // Hard delete — permanently removes the contribution from the database. No soft-cancel state;
    // nothing is left behind to undo. Rejected outright for an auto-generated (AutoFromBazar) row —
    // delete its originating Bazar purchase instead (see BazarPurchaseService.DeleteAsync).
    Task DeleteAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default);
}
