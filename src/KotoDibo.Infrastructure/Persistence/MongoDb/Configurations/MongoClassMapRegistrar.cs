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
            new BazarPurchaseConfiguration(),
            new ContributionConfiguration(),
            new DailyMealEntryConfiguration(),
            new BillSplitConfiguration(),
            new ExpenseConfiguration(),
            new BudgetConfiguration()
        ];

        foreach (var configuration in configurations)
        {
            configuration.Configure();
        }
    }
}
