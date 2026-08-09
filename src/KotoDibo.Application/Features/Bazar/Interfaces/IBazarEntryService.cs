using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Interfaces;

public interface IBazarEntryService
{
    Task<BazarEntryDto> CreateAsync(CreateBazarEntryRequest request, CancellationToken cancellationToken = default);

    Task<BazarEntryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BazarEntryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
