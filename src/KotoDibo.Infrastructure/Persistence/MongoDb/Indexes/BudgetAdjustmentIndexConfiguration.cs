using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class BudgetAdjustmentIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<BudgetAdjustment>(nameof(BudgetAdjustment));

        List<CreateIndexModel<BudgetAdjustment>> models =
        [
            // Adjustment history for one category allocation, newest first.
            new(Builders<BudgetAdjustment>.IndexKeys.Ascending(a => a.BudgetCategoryAllocationId).Descending(a => a.CreatedAt),
                new CreateIndexOptions { Name = "ix_budgetadjustment_allocationid_createdat" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
