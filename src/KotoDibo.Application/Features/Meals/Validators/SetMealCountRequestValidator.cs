using FluentValidation;
using KotoDibo.Application.Features.Meals.DTOs;

namespace KotoDibo.Application.Features.Meals.Validators;

public class SetMealCountRequestValidator : AbstractValidator<SetMealCountRequest>
{
    public SetMealCountRequestValidator()
    {
        // 0 is a valid, meaningful value — it explicitly excludes the member from that day's count
        // (distinct from having no entry at all), per MealCalculationService's semantics.
        RuleFor(x => x.Count).GreaterThanOrEqualTo(0).LessThanOrEqualTo(5);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
