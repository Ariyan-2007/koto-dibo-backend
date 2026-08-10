using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdDto> CreateAsync(string ownerUserId, CreateHouseholdRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdDto> GetByIdAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseholdDto>> GetMyHouseholdsAsync(string callerUserId, CancellationToken cancellationToken = default);

    Task<HouseholdDto> UpdateAsync(string householdId, string callerUserId, UpdateHouseholdRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdDto> ArchiveAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    Task<HouseholdDto> RestoreAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);
}
