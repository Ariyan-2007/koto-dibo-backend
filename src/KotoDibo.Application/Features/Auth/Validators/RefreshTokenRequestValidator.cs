using FluentValidation;
using KotoDibo.Application.Features.Auth.DTOs;

namespace KotoDibo.Application.Features.Auth.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}
