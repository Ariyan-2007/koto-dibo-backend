using FluentValidation;
using KotoDibo.Application.Features.BillSplit.DTOs;
using KotoDibo.Domain.Enums;

namespace KotoDibo.Application.Features.BillSplit.Validators;

public class CreateBillSplitRequestValidator : AbstractValidator<CreateBillSplitRequest>
{
    public CreateBillSplitRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);

        RuleFor(x => x.SplitMethod)
            .NotEmpty()
            .Must(value => Enum.TryParse<BillSplitMethod>(value, ignoreCase: true, out _))
            .WithMessage("SplitMethod must be one of: TariffMetered, EqualSplit, WeightedSplit.");

        RuleFor(x => x.PeriodFrom).NotEqual(default(DateOnly));
        RuleFor(x => x.PeriodTo)
            .NotEqual(default(DateOnly))
            .GreaterThanOrEqualTo(x => x.PeriodFrom)
            .WithMessage("PeriodTo must not be before PeriodFrom.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO 4217 code.");

        RuleFor(x => x.Notes).MaximumLength(500);

        When(x => IsMethod(x.SplitMethod, BillSplitMethod.TariffMetered), () =>
        {
            RuleFor(x => x.TariffCountry).NotEmpty().WithMessage("TariffCountry is required for TariffMetered bill splits.");
            RuleFor(x => x.MainMeterUsage)
                .NotNull().WithMessage("MainMeterUsage is required for TariffMetered bill splits.")
                .GreaterThanOrEqualTo(0m).When(x => x.MainMeterUsage is not null);
            RuleFor(x => x.MemberInputs).NotEmpty().WithMessage("At least one member sub-meter reading is required.");
            RuleForEach(x => x.MemberInputs).ChildRules(input =>
            {
                input.RuleFor(i => i.UserId).NotEmpty();
                input.RuleFor(i => i.Value).GreaterThanOrEqualTo(0m).WithMessage("Sub-meter usage cannot be negative.");
            });
            RuleFor(x => x)
                .Must(x => x.MemberInputs.Sum(i => i.Value) <= (x.MainMeterUsage ?? 0m))
                .WithMessage("Sum of member sub-meter usage cannot exceed MainMeterUsage.")
                .WithName(nameof(CreateBillSplitRequest.MemberInputs))
                .When(x => x.MainMeterUsage is not null && x.MemberInputs.Count > 0);
        });

        When(x => IsMethod(x.SplitMethod, BillSplitMethod.EqualSplit) || IsMethod(x.SplitMethod, BillSplitMethod.WeightedSplit), () =>
        {
            RuleFor(x => x.TotalAmount)
                .NotNull().WithMessage("TotalAmount is required for this split method.")
                .GreaterThan(0m).When(x => x.TotalAmount is not null);
        });

        When(x => IsMethod(x.SplitMethod, BillSplitMethod.WeightedSplit), () =>
        {
            RuleFor(x => x.MemberInputs).NotEmpty().WithMessage("At least one member weight is required for WeightedSplit.");
            RuleForEach(x => x.MemberInputs).ChildRules(input =>
            {
                input.RuleFor(i => i.UserId).NotEmpty();
                input.RuleFor(i => i.Value).GreaterThan(0m).WithMessage("Member weight must be greater than zero.");
            });
        });
    }

    private static bool IsMethod(string value, BillSplitMethod method)
        => Enum.TryParse<BillSplitMethod>(value, ignoreCase: true, out var parsed) && parsed == method;
}
