using FluentValidation;
using KotoDibo.Application.Features.Meals.DTOs;

namespace KotoDibo.Application.Features.Meals.Validators;

public class SetMealCountRequestValidator : AbstractValidator<SetMealCountRequest>
{
    public SetMealCountRequestValidator()
    {
        RuleFor(x => x.Count).GreaterThan(0).LessThanOrEqualTo(5);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
