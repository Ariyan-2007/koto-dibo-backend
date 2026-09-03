using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Validators;

public class AddBudgetCategoryRequestValidator : AbstractValidator<AddBudgetCategoryRequest>
{
    public AddBudgetCategoryRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PlannedAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
