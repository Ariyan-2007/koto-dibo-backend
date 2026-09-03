using FluentValidation;
using KotoDibo.Application.Features.Budget.DTOs;

namespace KotoDibo.Application.Features.Budget.Validators;

public class TransferBudgetCategoryRequestValidator : AbstractValidator<TransferBudgetCategoryRequest>
{
    public TransferBudgetCategoryRequestValidator()
    {
        RuleFor(x => x.ToCategoryAllocationId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
