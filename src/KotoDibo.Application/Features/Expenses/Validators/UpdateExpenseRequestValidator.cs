using FluentValidation;
using KotoDibo.Application.Features.Expenses.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Expenses.Validators;

public class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null);
        RuleFor(x => x.Currency)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.CategoryId).NotEmpty().When(x => x.CategoryId is not null);
        RuleFor(x => x.Merchant).MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).When(x => x.Date is not null);
        RuleFor(x => x.PaymentMethod)
            .Must(v => Enum.TryParse<ExpensePaymentMethod>(v, ignoreCase: true, out _))
            .When(x => x.PaymentMethod is not null)
            .WithMessage($"PaymentMethod must be one of: {string.Join(", ", Enum.GetNames<ExpensePaymentMethod>())}.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50).When(x => x.Tags is not null);
        RuleFor(x => x.Tags).Must(t => t == null || t.Count <= 20).WithMessage("A maximum of 20 tags is allowed.");
        RuleFor(x => x.ReceiptUrl).MaximumLength(2000);
    }
}
