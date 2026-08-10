using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class HouseholdIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<Household>(nameof(Household));

        // Supports "households owned by this user" lookups (e.g. blocking account deletion while
        // households are still owned, in a later phase).
        var model = new CreateIndexModel<Household>(
            Builders<Household>.IndexKeys.Ascending(h => h.OwnerUserId),
            new CreateIndexOptions { Name = "ix_household_owneruserid" });

        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
