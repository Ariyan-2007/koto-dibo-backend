namespace KotoDibo.Domain.Constants;

// Bangladesh (BDT) is this MVP's only market — see Application.Common.LocalDate's fixed UTC+6
// offset and TariffConfigSeeder's seeded BDT tariff. Personal Expense/Budget records let the
// caller override Currency per record (no conversion is performed anywhere), but default to BDT
// when omitted rather than forcing every request to repeat it.
public static class CurrencyDefaults
{
    public const string DefaultCurrency = "BDT";
}
