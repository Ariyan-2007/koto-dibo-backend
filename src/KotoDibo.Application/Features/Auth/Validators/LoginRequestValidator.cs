using FluentValidation;
using KotoDibo.Application.Features.Auth.DTOs;

namespace KotoDibo.Application.Features.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.DeviceId).MaximumLength(200);
        RuleFor(x => x.DeviceName).MaximumLength(200);
    }
}
