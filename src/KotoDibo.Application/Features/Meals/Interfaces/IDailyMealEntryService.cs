using KotoDibo.Application.Features.Meals.DTOs;

namespace KotoDibo.Application.Features.Meals.Interfaces;

public interface IDailyMealEntryService
{
    Task<DailyMealEntryDto> SetCountAsync(string householdId, string callerUserId, string targetUserId, DateOnly date, SetMealCountRequest request, CancellationToken cancellationToken = default);

    Task RemoveAsync(string householdId, string callerUserId, string targetUserId, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyMealEntryDto>> GetListAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? userId, CancellationToken cancellationToken = default);
}
