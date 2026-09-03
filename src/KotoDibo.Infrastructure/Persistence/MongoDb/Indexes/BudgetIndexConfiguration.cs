using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class BudgetIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<Budget>(nameof(Budget));

        List<CreateIndexModel<Budget>> models =
        [
            // List/period-range queries and "find the budget covering this date" lookups.
            new(Builders<Budget>.IndexKeys.Ascending(b => b.UserId).Ascending(b => b.StartDate),
                new CreateIndexOptions { Name = "ix_budget_userid_startdate" }),

            new(Builders<Budget>.IndexKeys.Ascending(b => b.UserId).Ascending(b => b.Status),
                new CreateIndexOptions { Name = "ix_budget_userid_status" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
