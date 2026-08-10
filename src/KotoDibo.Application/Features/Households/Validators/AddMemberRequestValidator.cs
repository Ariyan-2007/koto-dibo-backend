using FluentValidation;
using KotoDibo.Application.Features.Households.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Households.Validators;

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<HouseholdRole>(role, ignoreCase: true, out var parsed) && parsed != HouseholdRole.Owner)
            .WithMessage("Role must be one of: Manager, Member, Viewer.");
    }
}
