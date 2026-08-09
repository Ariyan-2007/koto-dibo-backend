using FluentValidation;
using KotoDibo.Application.Features.Bazar.DTOs;

namespace KotoDibo.Application.Features.Bazar.Validators;

public class CreateBazarEntryRequestValidator : AbstractValidator<CreateBazarEntryRequest>
{
    public CreateBazarEntryRequestValidator()
    {
    }
}
