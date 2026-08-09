using FluentValidation;
using KotoDibo.Application.Features.BillSplit.DTOs;

namespace KotoDibo.Application.Features.BillSplit.Validators;

public class CreateBillSplitRequestValidator : AbstractValidator<CreateBillSplitRequest>
{
    public CreateBillSplitRequestValidator()
    {
    }
}
