using FluentValidation;
using KotoDibo.Application.Features.Meals.DTOs;

namespace KotoDibo.Application.Features.Meals.Validators;

public class CreateMealEntryRequestValidator : AbstractValidator<CreateMealEntryRequest>
{
    public CreateMealEntryRequestValidator()
    {
    }
}
