using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Application.Features.Bazar.Interfaces;

namespace KotoDibo.Application.Features.Bazar.Services;

public class BazarEntryService : IBazarEntryService
{
    public Task<BazarEntryDto> CreateAsync(CreateBazarEntryRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<BazarEntryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<BazarEntryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
