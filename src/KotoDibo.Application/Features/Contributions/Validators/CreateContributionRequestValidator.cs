using FluentValidation;
using KotoDibo.Application.Features.Contributions.DTOs;

namespace KotoDibo.Application.Features.Contributions.Validators;

public class CreateContributionRequestValidator : AbstractValidator<CreateContributionRequest>
{
    public CreateContributionRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
