using FluentValidation;
using KotoDibo.Application.Features.Auth.DTOs;

namespace KotoDibo.Application.Features.Auth.Validators;

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
