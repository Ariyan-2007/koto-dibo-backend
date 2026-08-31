using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class BillSplitIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<BillSplit>(nameof(BillSplit));

        List<CreateIndexModel<BillSplit>> models =
        [
            // List/period-range queries for a household.
            new(Builders<BillSplit>.IndexKeys.Ascending(b => b.HouseholdId).Ascending(b => b.PeriodFrom),
                new CreateIndexOptions { Name = "ix_billsplit_householdid_periodfrom" }),

            // Active-only period-range scan — the settlement/list endpoints' main query.
            new(Builders<BillSplit>.IndexKeys.Ascending(b => b.HouseholdId).Ascending(b => b.Status).Ascending(b => b.PeriodFrom),
                new CreateIndexOptions { Name = "ix_billsplit_householdid_status_periodfrom" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
