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
            new MealEntryConfiguration(),
            new BazarEntryConfiguration(),
            new MealRateConfiguration(),
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
