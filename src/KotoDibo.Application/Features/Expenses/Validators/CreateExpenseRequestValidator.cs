using FluentValidation;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Expenses.Validators;

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3).When(x => x.Currency is not null);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Merchant).MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Date).NotEqual(default(DateOnly));
        RuleFor(x => x.PaymentMethod)
            .Must(BeAValidPaymentMethod)
            .When(x => x.PaymentMethod is not null)
            .WithMessage($"PaymentMethod must be one of: {string.Join(", ", Enum.GetNames<ExpensePaymentMethod>())}.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50).When(x => x.Tags is not null);
        RuleFor(x => x.Tags).Must(t => t == null || t.Count <= 20).WithMessage("A maximum of 20 tags is allowed.");
        RuleFor(x => x.ReceiptUrl).MaximumLength(2000);
    }

    private static bool BeAValidPaymentMethod(string? value) => value is not null && Enum.TryParse<ExpensePaymentMethod>(value, ignoreCase: true, out _);
}
