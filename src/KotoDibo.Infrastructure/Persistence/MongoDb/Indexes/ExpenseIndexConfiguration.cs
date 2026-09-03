using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class ExpenseIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<Expense>(nameof(Expense));

        List<CreateIndexModel<Expense>> models =
        [
            // Primary listing/dashboard query: a user's expenses within a date range.
            new(Builders<Expense>.IndexKeys.Ascending(e => e.UserId).Descending(e => e.Date),
                new CreateIndexOptions { Name = "ix_expense_userid_date" }),

            // Category-scoped date-range queries (budget-vs-actual per category).
            new(Builders<Expense>.IndexKeys.Ascending(e => e.UserId).Ascending(e => e.CategoryId).Ascending(e => e.Date),
                new CreateIndexOptions { Name = "ix_expense_userid_categoryid_date" }),

            // Status-scoped listing (Active-only is the default filter everywhere).
            new(Builders<Expense>.IndexKeys.Ascending(e => e.UserId).Ascending(e => e.Status).Descending(e => e.Date),
                new CreateIndexOptions { Name = "ix_expense_userid_status_date" }),

            // Guards RecurringExpenseGenerator idempotency at the storage layer: two generation
            // runs racing on the same due occurrence can't both insert the same (recurring, date) row.
            new(Builders<Expense>.IndexKeys.Ascending(e => e.RecurringExpenseId).Ascending(e => e.Date),
                new CreateIndexOptions<Expense>
                {
                    Name = "ux_expense_recurringexpenseid_date",
                    Unique = true,
                    PartialFilterExpression = Builders<Expense>.Filter.Ne(e => e.RecurringExpenseId, null),
                }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
