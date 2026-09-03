using FluentValidation;
using KotoDibo.Application.Features.ExpenseCategories.DTOs;

namespace KotoDibo.Application.Features.ExpenseCategories.Validators;

public class UpdateExpenseCategoryRequestValidator : AbstractValidator<UpdateExpenseCategoryRequest>
{
    public UpdateExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Icon).MaximumLength(50);
    }
}
