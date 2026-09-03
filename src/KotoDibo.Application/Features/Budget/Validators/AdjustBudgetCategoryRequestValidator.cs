using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Validators;

public class AdjustBudgetCategoryRequestValidator : AbstractValidator<AdjustBudgetCategoryRequest>
{
    public AdjustBudgetCategoryRequestValidator()
    {
        RuleFor(x => x.Delta).NotEqual(0m).WithMessage("Delta must be non-zero.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
