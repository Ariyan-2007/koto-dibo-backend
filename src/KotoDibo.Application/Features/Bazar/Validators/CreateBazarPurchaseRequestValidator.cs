using FluentValidation;
using KotoDibo.Application.Features.Bazar.DTOs;
using KotoDibo.Domain.Enums;

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

        RuleFor(x => x.FundingSource)
            .Must(value => Enum.TryParse<BazarFundingSource>(value, ignoreCase: true, out _))
            .WithMessage("FundingSource must be either 'Personal' or 'HouseholdFund'.");

        // A negative entry is a leftover/correction adjustment, not a real purchase — there's no
        // sense in which it can be "paid from the shared fund".
        RuleFor(x => x.FundingSource)
            .Must(value => Enum.TryParse<BazarFundingSource>(value, ignoreCase: true, out var parsed) && parsed == BazarFundingSource.Personal)
            .When(x => x.Amount < 0)
            .WithMessage("A negative (leftover) amount can only use FundingSource 'Personal'.");
    }
}
