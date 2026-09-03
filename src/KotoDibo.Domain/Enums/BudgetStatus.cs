namespace KotoDibo.Domain.Enums;

// Lifecycle of a single Budget (one period's envelope, e.g. "January 2026"). Draft lets a user
// plan allocations before committing; Active is the currently-tracked period; Completed marks a
// past period whose window has ended; Archived hides it from default listings without deleting
// financial history.
public enum BudgetStatus
{
    Draft,
    Active,
    Completed,
    Archived,
}
