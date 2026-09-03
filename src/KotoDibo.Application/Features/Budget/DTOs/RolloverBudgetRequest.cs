namespace KotoDibo.Application.Features.Budget.DTOs;

// Creates the next period's Budget from the current one. StartDate/EndDate are auto-computed from
// the current budget's PeriodType/EndDate when omitted (e.g. a Monthly budget rolls into the very
// next calendar month) — only Custom-period budgets require both to be given explicitly.
public record RolloverBudgetRequest
{
    public string? Name { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}
