using KotoDibo.Application.Features.Meals.DTOs;
using KotoDibo.Application.Features.Meals.Interfaces;

namespace KotoDibo.Application.Features.Meals.Services;

public class MealEntryService : IMealEntryService
{
    public Task<MealEntryDto> CreateAsync(CreateMealEntryRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<MealEntryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<MealEntryDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
