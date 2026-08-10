using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class DailyMealEntryIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<DailyMealEntry>(nameof(DailyMealEntry));

        List<CreateIndexModel<DailyMealEntry>> models =
        [
            // One active entry per member per day — backs upsert-by-PUT semantics. Ordered
            // (HouseholdId, Date, UserId) so it also serves the calculation engine's
            // "all of this household's meal entries in a date range" query as a prefix scan.
            new(Builders<DailyMealEntry>.IndexKeys.Ascending(e => e.HouseholdId).Ascending(e => e.Date).Ascending(e => e.UserId),
                new CreateIndexOptions<DailyMealEntry>
                {
                    Unique = true,
                    Name = "ux_dailymealentry_household_date_user_active",
                    PartialFilterExpression = Builders<DailyMealEntry>.Filter.Eq(e => e.Status, DailyMealEntryStatus.Active),
                }),

            // "My meal history".
            new(Builders<DailyMealEntry>.IndexKeys.Ascending(e => e.HouseholdId).Ascending(e => e.UserId).Ascending(e => e.Date),
                new CreateIndexOptions { Name = "ix_dailymealentry_householdid_userid_date" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
