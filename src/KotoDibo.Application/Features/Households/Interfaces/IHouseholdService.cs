using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdDto> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken = default);

    Task<HouseholdDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseholdDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
