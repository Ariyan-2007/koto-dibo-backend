using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Budget.Validators;

public class CreateBudgetRequestValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.PeriodType)
            .NotEmpty()
            .Must(v => Enum.TryParse<BudgetPeriodType>(v, ignoreCase: true, out _))
            .WithMessage($"PeriodType must be one of: {string.Join(", ", Enum.GetNames<BudgetPeriodType>())}.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .NotNull()
            .WithMessage("EndDate is required when PeriodType is Custom.")
            .When(x => string.Equals(x.PeriodType, nameof(BudgetPeriodType.Custom), StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("EndDate cannot be before StartDate.");
        RuleFor(x => x.Notes).MaximumLength(1000);

        RuleForEach(x => x.Categories).ChildRules(category =>
        {
            category.RuleFor(c => c.CategoryId).NotEmpty();
            category.RuleFor(c => c.PlannedAmount).GreaterThanOrEqualTo(0);
            category.RuleFor(c => c.Notes).MaximumLength(500);
        });

        // BudgetCategoryAllocation has a unique (BudgetId, CategoryId) index — a duplicate here
        // would insert the budget and the first allocation, then fail partway through the rest
        // with a raw Conflict instead of rejecting the whole request upfront.
        RuleFor(x => x.Categories)
            .Must(categories => categories!.Select(c => c.CategoryId).Distinct().Count() == categories!.Count)
            .WithMessage("Categories cannot contain duplicate CategoryId entries.")
            .When(x => x.Categories is { Count: > 0 });
    }
}
