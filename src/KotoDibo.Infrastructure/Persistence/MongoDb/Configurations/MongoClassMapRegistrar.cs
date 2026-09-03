namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public static class MongoClassMapRegistrar
{
    public static void RegisterAll()
    {
        IMongoClassMapConfiguration[] configurations =
        [
            new UserConfiguration(),
            new UserCredentialConfiguration(),
            new RefreshTokenConfiguration(),
            new HouseholdConfiguration(),
            new HouseholdMembershipConfiguration(),
            new HouseholdInviteConfiguration(),
            new BazarPurchaseConfiguration(),
            new ContributionConfiguration(),
            new DailyMealEntryConfiguration(),
            new BillSplitConfiguration(),
            new UtilityTariffConfigConfiguration(),
            new ExpenseConfiguration(),
            new BudgetConfiguration(),
            new ExpenseCategoryConfiguration(),
            new RecurringExpenseConfiguration(),
            new BudgetCategoryAllocationConfiguration(),
            new BudgetAdjustmentConfiguration(),
        ];

        foreach (var configuration in configurations)
        {
            configuration.Configure();
        }
    }
}
