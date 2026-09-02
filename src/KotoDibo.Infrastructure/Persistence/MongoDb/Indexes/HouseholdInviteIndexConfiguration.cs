using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class HouseholdInviteIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<HouseholdInvite>(nameof(HouseholdInvite));

        List<CreateIndexModel<HouseholdInvite>> models =
        [
            // Codes are globally unique across their whole lifetime (not just while Pending) so a
            // redeemed/expired/revoked code can never be confused with a live one.
            new(Builders<HouseholdInvite>.IndexKeys.Ascending(i => i.Code),
                new CreateIndexOptions { Unique = true, Name = "ux_householdinvite_code" }),

            // "Pending invites for this household" (management list view).
            new(Builders<HouseholdInvite>.IndexKeys.Ascending(i => i.HouseholdId).Ascending(i => i.Status),
                new CreateIndexOptions { Name = "ix_householdinvite_householdid_status" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
