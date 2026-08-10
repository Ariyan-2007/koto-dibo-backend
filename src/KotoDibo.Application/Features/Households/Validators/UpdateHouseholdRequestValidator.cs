using FluentValidation;
using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Validators;

public class UpdateHouseholdRequestValidator : AbstractValidator<UpdateHouseholdRequest>
{
    public UpdateHouseholdRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.Type)
            .MaximumLength(50);
    }
}
