using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class WithdrawalIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<Withdrawal>(nameof(Withdrawal));

        List<CreateIndexModel<Withdrawal>> models =
        [
            new(Builders<Withdrawal>.IndexKeys.Ascending(w => w.HouseholdId).Ascending(w => w.Date),
                new CreateIndexOptions { Name = "ix_withdrawal_householdid_date" }),

            new(Builders<Withdrawal>.IndexKeys.Ascending(w => w.HouseholdId).Ascending(w => w.WithdrawnByUserId).Ascending(w => w.Date),
                new CreateIndexOptions { Name = "ix_withdrawal_householdid_withdrawnby_date" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
