using FluentValidation;
using KotoDibo.Application.Features.Expenses.DTOs;

namespace KotoDibo.Application.Features.Expenses.Validators;

public class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator()
    {
    }
}
