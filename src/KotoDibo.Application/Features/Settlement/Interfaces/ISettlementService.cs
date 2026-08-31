using KotoDibo.Application.Features.Settlement.DTOs;

namespace KotoDibo.Application.Features.Settlement.Interfaces;

public interface ISettlementService
{
    Task<HouseholdSettlementDto> GetSettlementAsync(string householdId, string callerUserId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
