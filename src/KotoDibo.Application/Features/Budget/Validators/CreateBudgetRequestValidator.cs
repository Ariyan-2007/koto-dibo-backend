using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Validators;

public class CreateBudgetRequestValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator()
    {
        RuleFor(x => x.Period)
            .NotEmpty()
            .Matches("^\\d{4}-(0[1-9]|1[0-2])$")
            .WithMessage("Period must be in 'YYYY-MM' format.");
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
