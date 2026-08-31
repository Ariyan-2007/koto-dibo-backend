using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// A household + billing-period record for a shared bill. Shape mirrors BazarPurchase/Contribution's
// lifecycle (Active/Cancelled, never hard-deleted). Settlement (who owes what) is computed on demand
// by FairSplitAllocator from these inputs, the same way MealCalculationService computes meal rates
// on demand from DailyMealEntry rows rather than persisting a derived total.
public class BillSplit
{
    public string Id { get; set; } = string.Empty;
    public string HouseholdId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public BillSplitMethod SplitMethod { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string Currency { get; set; } = string.Empty;

    // TariffMetered only: which centrally-seeded tariff schedule to bill against.
    public string? TariffCountry { get; set; }
    public string? TariffProvider { get; set; }

    // TariffMetered: total main-meter usage for the period. Null for EqualSplit/WeightedSplit.
    public decimal? MainMeterUsage { get; set; }

    // EqualSplit/WeightedSplit: the bill total to divide. TariffMetered derives its total from the
    // tariff bands at settlement time instead, so this stays null for that method.
    public decimal? TotalAmount { get; set; }

    // TariffMetered: per-member sub-meter usage. WeightedSplit: per-member fixed weight/share.
    // Unused (empty) for EqualSplit, which divides across the household's current active members.
    public List<BillSplitMemberInput> MemberInputs { get; set; } = [];

    public string? Notes { get; set; }
    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BillSplitMemberInput
{
    public string UserId { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
