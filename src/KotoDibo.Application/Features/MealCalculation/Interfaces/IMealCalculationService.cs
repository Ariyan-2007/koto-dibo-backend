using KotoDibo.Application.Features.MealCalculation.DTOs;

namespace KotoDibo.Application.Features.MealCalculation.Interfaces;

public interface IMealCalculationService
{
    Task<MealCalculationDto> GetMealRateAsync(string householdId, string callerUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
