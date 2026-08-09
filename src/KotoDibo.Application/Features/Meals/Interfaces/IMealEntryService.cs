using KotoDibo.Application.Features.Meals.DTOs;

namespace KotoDibo.Application.Features.Meals.Interfaces;

public interface IMealEntryService
{
    Task<MealEntryDto> CreateAsync(CreateMealEntryRequest request, CancellationToken cancellationToken = default);

    Task<MealEntryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealEntryDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
