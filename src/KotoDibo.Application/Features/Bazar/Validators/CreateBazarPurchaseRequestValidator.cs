using FluentValidation;
using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Validators;

public class CreateBazarPurchaseRequestValidator : AbstractValidator<CreateBazarPurchaseRequest>
{
    public CreateBazarPurchaseRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly));

        // Negative amounts are allowed on purpose: an entry like "-700, Leftover" is how unspent
        // shopping cash held over to next month is recorded — it deflates this month's FoodCost
        // (MealCalculationService just sums Amount) without needing a separate concept. Only exactly
        // zero is meaningless as a purchase and rejected.
        RuleFor(x => x.Amount).NotEqual(0);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
