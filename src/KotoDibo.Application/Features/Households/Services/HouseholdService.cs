using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Application.Features.Households.Interfaces;

namespace KotoDibo.Application.Features.Households.Services;

public class HouseholdService : IHouseholdService
{
    public Task<HouseholdDto> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<HouseholdDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<HouseholdDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
