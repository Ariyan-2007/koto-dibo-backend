using FluentValidation;
using KotoDibo.Application.Features.RecurringExpenses.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.RecurringExpenses.Validators;

public class CreateRecurringExpenseRequestValidator : AbstractValidator<CreateRecurringExpenseRequest>
{
    public CreateRecurringExpenseRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Merchant).MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.PaymentMethod)
            .Must(v => Enum.TryParse<ExpensePaymentMethod>(v, ignoreCase: true, out _))
            .When(x => x.PaymentMethod is not null)
            .WithMessage($"PaymentMethod must be one of: {string.Join(", ", Enum.GetNames<ExpensePaymentMethod>())}.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50).When(x => x.Tags is not null);
        RuleFor(x => x.Frequency)
            .NotEmpty()
            .Must(v => Enum.TryParse<RecurrenceFrequency>(v, ignoreCase: true, out _))
            .WithMessage($"Frequency must be one of: {string.Join(", ", Enum.GetNames<RecurrenceFrequency>())}.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("EndDate cannot be before StartDate.");
    }
}
