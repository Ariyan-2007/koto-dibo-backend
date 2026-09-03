using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class BudgetCategoryAllocationIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<BudgetCategoryAllocation>(nameof(BudgetCategoryAllocation));

        List<CreateIndexModel<BudgetCategoryAllocation>> models =
        [
            // Fetching all category allocations for a budget (the budget detail endpoint's main query).
            new(Builders<BudgetCategoryAllocation>.IndexKeys.Ascending(a => a.BudgetId),
                new CreateIndexOptions { Name = "ix_budgetcategoryallocation_budgetid" }),

            // One allocation per category per budget.
            new(Builders<BudgetCategoryAllocation>.IndexKeys.Ascending(a => a.BudgetId).Ascending(a => a.CategoryId),
                new CreateIndexOptions { Name = "ux_budgetcategoryallocation_budgetid_categoryid", Unique = true }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
