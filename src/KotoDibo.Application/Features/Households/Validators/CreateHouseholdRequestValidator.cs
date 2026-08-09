using FluentValidation;
using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Validators;

public class CreateHouseholdRequestValidator : AbstractValidator<CreateHouseholdRequest>
{
    public CreateHouseholdRequestValidator()
    {
    }
}
