namespace KotoDibo.Application.Features.HouseholdBalance.DTOs;

// A single Contribution or BazarPurchase row, reshaped into one common ledger view so the frontend
// can render "current balance + its transaction history" from one endpoint instead of merging two
// lists client-side. Nothing here is stored separately — GetTransactionsAsync projects it live from
// the same Contribution/BazarPurchase rows that back HouseholdBalanceDto and the Bazar/Contribution
// list endpoints, so there's no second source of truth to keep in sync.
public record HouseholdLedgerTransactionDto
{
    public string Id { get; init; } = string.Empty;
    public string HouseholdId { get; init; } = string.Empty;

    // "Contribution" or "BazarPurchase".
    public string EntryType { get; init; } = string.Empty;

    // "In" (added to the pool) or "Out" (drawn from it). A personal-pocket Bazar purchase never
    // appears as "Out" here in a way that double-counts — see BalanceImpact.
    public string Direction { get; init; } = string.Empty;

    // The entry's actual effect on CurrentBalance: +Amount for every Contribution, -Amount only for
    // a Bazar purchase funded from the household pool. A personal-pocket Bazar purchase carries
    // BalanceImpact 0 here (its own -Amount is offset by its mirrored Contribution's +Amount, which
    // appears as its own separate row) — this field exists so a client can sanity-check
    // CurrentBalance == sum(BalanceImpact) without re-deriving the funding-source rules itself.
    public decimal BalanceImpact { get; init; }

    public DateOnly Date { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;

    // The member this entry is financially attributed to (PurchasedByUserId / ContributedByUserId).
    public string UserId { get; init; } = string.Empty;

    // Who actually submitted the record — may differ from UserId (on-behalf-of / auto-generated).
    public string CreatedByUserId { get; init; } = string.Empty;

    // BazarFundingSource ("Personal"/"HouseholdFund") for a BazarPurchase row, or
    // ContributionSourceType ("Manual"/"AutoFromBazar") for a Contribution row.
    public string SourceType { get; init; } = string.Empty;

    // The counterpart record's id: a personal-pocket BazarPurchase row's mirrored Contribution id,
    // or an AutoFromBazar Contribution row's originating BazarPurchase id. Null otherwise.
    public string? LinkedEntryId { get; init; }

    public string? Note { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
