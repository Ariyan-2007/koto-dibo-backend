using FluentValidation;
using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Validators;

public class CreateBazarPurchaseRequestValidator : AbstractValidator<CreateBazarPurchaseRequest>
{
    public CreateBazarPurchaseRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
