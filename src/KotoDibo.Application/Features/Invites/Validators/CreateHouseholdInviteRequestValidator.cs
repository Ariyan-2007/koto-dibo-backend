using FluentValidation;
using KotoDibo.Application.Features.Invites.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Invites.Validators;

public class CreateHouseholdInviteRequestValidator : AbstractValidator<CreateHouseholdInviteRequest>
{
    public CreateHouseholdInviteRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<HouseholdRole>(role, ignoreCase: true, out var parsed) && parsed != HouseholdRole.Owner)
            .WithMessage("Role must be one of: Manager, Member, Viewer.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .Must(baseUrl => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("BaseUrl must be an absolute http(s) URL.");

        RuleFor(x => x.ExpiresInHours)
            .InclusiveBetween(1, 720)
            .When(x => x.ExpiresInHours.HasValue)
            .WithMessage("ExpiresInHours must be between 1 and 720 (30 days).");
    }
}
