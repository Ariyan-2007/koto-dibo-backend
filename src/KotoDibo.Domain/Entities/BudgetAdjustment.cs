using KotoDibo.Domain.Enums;

namespace KotoDibo.Domain.Entities;

// Immutable audit row for every change to a BudgetCategoryAllocation's PlannedAmount/RolloverAmount
// — a PATCH to the allocation never just overwrites the figure, it also appends one of these so
// "why did this category's budget change" stays answerable later.
public class BudgetAdjustment
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public string BudgetCategoryAllocationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public BudgetAdjustmentType Type { get; set; }

    // Signed delta applied to PlannedAmount (or RolloverAmount for Type == Rollover).
    public decimal Amount { get; set; }

    // PlannedAmount + RolloverAmount immediately after this adjustment was applied.
    public decimal BalanceAfter { get; set; }

    // For TransferIn/TransferOut, the allocation on the other side of the transfer.
    public string? RelatedCategoryAllocationId { get; set; }

    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
