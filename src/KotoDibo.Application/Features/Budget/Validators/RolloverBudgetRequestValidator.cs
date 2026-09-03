using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Validators;

public class RolloverBudgetRequestValidator : AbstractValidator<RolloverBudgetRequest>
{
    public RolloverBudgetRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(150);
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate is not null && x.EndDate is not null)
            .WithMessage("EndDate cannot be before StartDate.");
    }
}
