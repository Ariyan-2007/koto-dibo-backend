using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class ExpenseCategoryIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<ExpenseCategory>(nameof(ExpenseCategory));

        List<CreateIndexModel<ExpenseCategory>> models =
        [
            // "my categories + system defaults" listing.
            new(Builders<ExpenseCategory>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.IsActive),
                new CreateIndexOptions { Name = "ix_expensecategory_userid_isactive" }),

            // Subcategory lookups under a parent.
            new(Builders<ExpenseCategory>.IndexKeys.Ascending(c => c.ParentCategoryId),
                new CreateIndexOptions { Name = "ix_expensecategory_parentcategoryid" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
