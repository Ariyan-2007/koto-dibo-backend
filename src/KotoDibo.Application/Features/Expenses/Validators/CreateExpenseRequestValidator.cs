using FluentValidation;
using KotoDibo.Application.Features.Expenses.DTOs;

namespace KotoDibo.Application.Features.Expenses.Validators;

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
    }
}
