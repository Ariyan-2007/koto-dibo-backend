using KotoDibo.Application.Features.HouseholdBalance.DTOs;

namespace KotoDibo.Application.Features.HouseholdBalance.Interfaces;

public interface IHouseholdBalanceService
{
    Task<HouseholdBalanceDto> GetBalanceAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    // No permission check — for server-side use only (e.g. BazarPurchaseService's overdraft
    // check), where the caller has already been authorized for the action it's guarding.
    Task<decimal> GetCurrentBalanceAsync(string householdId, CancellationToken cancellationToken = default);
}
