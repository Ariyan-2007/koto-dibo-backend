namespace KotoDibo.Domain.Enums;

// Why a BudgetCategoryAllocation's PlannedAmount or RolloverAmount changed. Recorded as an
// immutable BudgetAdjustment row per change so allocation history stays auditable instead of a
// PlannedAmount edit silently overwriting the prior value with no trace.
public enum BudgetAdjustmentType
{
    Initial,
    Increase,
    Decrease,
    Rollover,
    TransferIn,
    TransferOut,
}
