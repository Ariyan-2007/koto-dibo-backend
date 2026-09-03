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
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
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
    }
}
