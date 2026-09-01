using FluentValidation;
using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Validators;

public class UpdateBazarPurchaseRequestValidator : AbstractValidator<UpdateBazarPurchaseRequest>
{
    public UpdateBazarPurchaseRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).When(x => x.Date is not null);
        // See CreateBazarPurchaseRequestValidator — negative amounts are valid (leftover/correction
        // entries), only exactly zero is rejected.
        RuleFor(x => x.Amount).NotEqual(0).When(x => x.Amount is not null);
        RuleFor(x => x.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .When(x => x.Currency is not null);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
