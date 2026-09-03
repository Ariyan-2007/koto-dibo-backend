using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class RecurringExpenseIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<RecurringExpense>(nameof(RecurringExpense));

        List<CreateIndexModel<RecurringExpense>> models =
        [
            new(Builders<RecurringExpense>.IndexKeys.Ascending(r => r.UserId).Ascending(r => r.IsActive),
                new CreateIndexOptions { Name = "ix_recurringexpense_userid_isactive" }),

            // Due-occurrence sweep: active recurring expenses ordered by how soon they're due.
            new(Builders<RecurringExpense>.IndexKeys.Ascending(r => r.IsActive).Ascending(r => r.NextOccurrenceDate),
                new CreateIndexOptions { Name = "ix_recurringexpense_isactive_nextoccurrencedate" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
