namespace KotoDibo.Domain.Enums;

// Shared by BazarPurchase and Contribution — both are simple money-in/money-out ledger entries
// with identical lifecycle (record it, optionally cancel it; never hard-deleted for auditability).
public enum FinancialEntryStatus
{
    Active,
    Cancelled,
}
