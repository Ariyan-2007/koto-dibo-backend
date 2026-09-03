namespace KotoDibo.Domain.Constants;

// Centralized so category/overall health status derivations (BudgetCalculator) don't scatter
// magic numbers across services — tune budgeting sensitivity in exactly one place.
public static class BudgetThresholds
{
    // A category crosses from OnTrack to Warning once usage reaches this percentage of its total
    // available amount (planned + rollover). 100%+ is always Overspent regardless of this value.
    public const decimal CategoryWarningPercentage = 80m;

    // A category is flagged in dashboard insights as "significantly under budget" below this usage.
    public const decimal CategoryUnderUsedPercentage = 50m;

    // Overall budget health mirrors the category thresholds for Warning, but Overspending/Critical
    // need their own bar since "the whole month" tolerates less slack than any single envelope.
    public const decimal OverallWarningPercentage = 80m;
    public const decimal OverallCriticalPercentage = 120m;

    // Overall health escalates to Critical once this many categories are individually overspent,
    // even if the blended overall percentage hasn't crossed OverallCriticalPercentage yet.
    public const int CriticalOverspentCategoryCount = 3;

    // Period-over-period spending swings within this band (either direction) read as "Stable"
    // rather than Increased/Decreased — avoids a dashboard calling a 0.4% wobble a trend.
    public const decimal StableSpendingChangePercentage = 2m;
}
