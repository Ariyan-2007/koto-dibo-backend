using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.Budget.Validators;

public class UpdateBudgetRequestValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Status)
            .Must(v => Enum.TryParse<BudgetStatus>(v, ignoreCase: true, out _))
            .When(x => x.Status is not null)
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<BudgetStatus>())}.");
    }
}
