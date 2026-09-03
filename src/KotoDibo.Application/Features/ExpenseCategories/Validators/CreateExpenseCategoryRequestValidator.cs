using FluentValidation;
using KotoDibo.Application.Features.ExpenseCategories.DTOs;

namespace KotoDibo.Application.Features.ExpenseCategories.Validators;

public class CreateExpenseCategoryRequestValidator : AbstractValidator<CreateExpenseCategoryRequest>
{
    public CreateExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Icon).MaximumLength(50);
    }
}
