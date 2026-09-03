namespace KotoDibo.Domain.Enums;

// How a Budget's StartDate/EndDate window is derived. Monthly/Weekly/Yearly are convenience
// shapes the API can auto-compute EndDate from StartDate for; Custom requires an explicit EndDate
// (e.g. a trip budget spanning a handful of days).
public enum BudgetPeriodType
{
    Weekly,
    Monthly,
    Yearly,
    Custom,
}
