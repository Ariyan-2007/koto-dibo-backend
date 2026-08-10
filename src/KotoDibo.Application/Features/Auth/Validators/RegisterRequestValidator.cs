using FluentValidation;
using KotoDibo.Application.Features.Auth.DTOs;

namespace KotoDibo.Application.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[\p{L}\p{M} .'-]+$")
            .WithMessage("Name may only contain letters, spaces, hyphens, apostrophes and periods.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.DeviceId).MaximumLength(200);
        RuleFor(x => x.DeviceName).MaximumLength(200);
    }
}
