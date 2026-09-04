using KotoDibo.Application.Features.HouseholdBalance.DTOs;

namespace KotoDibo.Application.Features.HouseholdBalance.Interfaces;

public interface IHouseholdBalanceService
{
    Task<HouseholdBalanceDto> GetBalanceAsync(string householdId, string callerUserId, CancellationToken cancellationToken = default);

    // The Contribution/BazarPurchase rows behind the balance, merged into one chronological ledger.
    // status defaults to Active-only when omitted, matching GetBalanceAsync's own semantics.
    Task<IReadOnlyList<HouseholdLedgerTransactionDto>> GetTransactionsAsync(string householdId, string callerUserId, DateOnly? from, DateOnly? to, string? status, CancellationToken cancellationToken = default);

    // No permission check — for server-side use only (e.g. BazarPurchaseService's overdraft
    // check), where the caller has already been authorized for the action it's guarding.
    Task<decimal> GetCurrentBalanceAsync(string householdId, CancellationToken cancellationToken = default);
}
