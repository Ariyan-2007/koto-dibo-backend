using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class BazarPurchaseIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<BazarPurchase>(nameof(BazarPurchase));

        List<CreateIndexModel<BazarPurchase>> models =
        [
            // List/date-range queries for a household.
            new(Builders<BazarPurchase>.IndexKeys.Ascending(p => p.HouseholdId).Ascending(p => p.Date),
                new CreateIndexOptions { Name = "ix_bazarpurchase_householdid_date" }),

            // "My purchases" filter.
            new(Builders<BazarPurchase>.IndexKeys.Ascending(p => p.HouseholdId).Ascending(p => p.PurchasedByUserId).Ascending(p => p.Date),
                new CreateIndexOptions { Name = "ix_bazarpurchase_householdid_purchasedby_date" }),

            // Active-only date-range scan — the calculation engine's main query.
            new(Builders<BazarPurchase>.IndexKeys.Ascending(p => p.HouseholdId).Ascending(p => p.Status).Ascending(p => p.Date),
                new CreateIndexOptions { Name = "ix_bazarpurchase_householdid_status_date" }),

            // Household balance calculation — all-time (no date bound), filtered to fund-funded spend.
            new(Builders<BazarPurchase>.IndexKeys.Ascending(p => p.HouseholdId).Ascending(p => p.Status).Ascending(p => p.FundingSource),
                new CreateIndexOptions { Name = "ix_bazarpurchase_householdid_status_fundingsource" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
