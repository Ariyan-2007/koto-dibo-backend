namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public static class MongoIndexInitializer
{
    public static async Task InitializeAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        IMongoIndexConfiguration[] configurations =
        [
            new UserIndexConfiguration(),
            new UserCredentialIndexConfiguration(),
            new RefreshTokenIndexConfiguration(),
            new HouseholdIndexConfiguration(),
            new HouseholdMembershipIndexConfiguration(),
        ];

        foreach (var configuration in configurations)
        {
            await configuration.ConfigureIndexesAsync(context, cancellationToken);
        }
    }
}
