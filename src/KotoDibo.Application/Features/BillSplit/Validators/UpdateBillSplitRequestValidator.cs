using FluentValidation;
using KotoDibo.Application.Features.BillSplit.DTOs;

namespace KotoDibo.Application.Features.BillSplit.Validators;

// Cross-field checks that depend on the existing record's SplitMethod (e.g. "sub-meter usage sum
// cannot exceed MainMeterUsage") are enforced in BillSplitService, which has that context; this
// validator only checks constraints that hold regardless of method.
public class UpdateBillSplitRequestValidator : AbstractValidator<UpdateBillSplitRequest>
{
    public UpdateBillSplitRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200).When(x => x.Title is not null);
        RuleFor(x => x.MainMeterUsage).GreaterThanOrEqualTo(0m).When(x => x.MainMeterUsage is not null);
        RuleFor(x => x.TotalAmount).GreaterThan(0m).When(x => x.TotalAmount is not null);
        RuleFor(x => x.Notes).MaximumLength(500);

        RuleForEach(x => x.MemberInputs).ChildRules(input =>
        {
            input.RuleFor(i => i.UserId).NotEmpty();
            input.RuleFor(i => i.Value).GreaterThanOrEqualTo(0m);
        }).When(x => x.MemberInputs is not null);

        // See CreateBillSplitRequestValidator: a duplicate UserId would otherwise crash
        // BillSplitService's settlement calculation with an unhandled ArgumentException.
        RuleFor(x => x.MemberInputs)
            .Must(inputs => inputs!.Select(i => i.UserId).Distinct().Count() == inputs!.Count)
            .WithMessage("MemberInputs cannot contain duplicate UserId entries.")
            .When(x => x.MemberInputs is not null);

        RuleForEach(x => x.FixedCharges).ChildRules(charge =>
        {
            charge.RuleFor(c => c.Label).NotEmpty().MaximumLength(100);
            charge.RuleFor(c => c.Amount).GreaterThan(0m).WithMessage("Fixed charge amount must be greater than zero.");
        }).When(x => x.FixedCharges is not null);
    }
}
