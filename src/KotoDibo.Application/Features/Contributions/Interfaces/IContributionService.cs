using KotoDibo.Application.Features.Contributions.DTOs;

namespace KotoDibo.Application.Features.Contributions.Interfaces;

public interface IContributionService
{
    Task<ContributionDto> CreateAsync(string householdId, string callerUserId, string targetUserId, CreateContributionRequest request, CancellationToken cancellationToken = default);

    Task<ContributionDto> GetByIdAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContributionDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default);

    Task<ContributionDto> UpdateAsync(string householdId, string callerUserId, string contributionId, UpdateContributionRequest request, CancellationToken cancellationToken = default);

    Task<ContributionDto> CancelAsync(string householdId, string callerUserId, string contributionId, CancellationToken cancellationToken = default);
}
