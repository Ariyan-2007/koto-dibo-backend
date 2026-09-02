using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class ContributionIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<Contribution>(nameof(Contribution));

        List<CreateIndexModel<Contribution>> models =
        [
            new(Builders<Contribution>.IndexKeys.Ascending(c => c.HouseholdId).Ascending(c => c.Date),
                new CreateIndexOptions { Name = "ix_contribution_householdid_date" }),

            new(Builders<Contribution>.IndexKeys.Ascending(c => c.HouseholdId).Ascending(c => c.ContributedByUserId).Ascending(c => c.Date),
                new CreateIndexOptions { Name = "ix_contribution_householdid_contributedby_date" }),

            // Active-only date-range scan — the calculation engine's main query.
            new(Builders<Contribution>.IndexKeys.Ascending(c => c.HouseholdId).Ascending(c => c.Status).Ascending(c => c.Date),
                new CreateIndexOptions { Name = "ix_contribution_householdid_status_date" }),

            // Cascade lookups from BazarPurchase.LinkedContributionId's reverse side (find/verify
            // the mirrored row for a given purchase). Sparse: most contributions are Manual and
            // have no SourceBazarPurchaseId.
            new(Builders<Contribution>.IndexKeys.Ascending(c => c.SourceBazarPurchaseId),
                new CreateIndexOptions { Name = "ix_contribution_sourcebazarpurchaseid", Sparse = true }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
