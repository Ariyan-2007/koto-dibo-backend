using FluentValidation;
using KotoDibo.Application.Features.Households.DTOs;

namespace KotoDibo.Application.Features.Households.Validators;

public class TransferOwnershipRequestValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator()
    {
        RuleFor(x => x.NewOwnerUserId)
            .NotEmpty();
    }
}
