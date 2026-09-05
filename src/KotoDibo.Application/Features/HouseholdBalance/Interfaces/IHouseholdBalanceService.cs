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

    // The currency this household's financial entries are already recorded in, taken from any
    // existing active Contribution/BazarPurchase row — null if the household has none yet, in
    // which case the first entry's currency establishes it. Every financial entry (Contribution,
    // BazarPurchase, BillSplit) must agree with this, or Amount fields from different currencies
    // would get summed together as if they were the same money — see HouseholdBalanceCalculator.
    // No permission check — for server-side use only, same as GetCurrentBalanceAsync.
    Task<string?> GetEstablishedCurrencyAsync(string householdId, CancellationToken cancellationToken = default);
}
