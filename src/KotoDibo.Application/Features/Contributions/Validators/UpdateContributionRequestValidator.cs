using FluentValidation;
using KotoDibo.Application.Features.Contributions.DTOs;

namespace KotoDibo.Application.Features.Contributions.Validators;

public class UpdateContributionRequestValidator : AbstractValidator<UpdateContributionRequest>
{
    public UpdateContributionRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).When(x => x.Date is not null);
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null);
        RuleFor(x => x.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .When(x => x.Currency is not null);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
