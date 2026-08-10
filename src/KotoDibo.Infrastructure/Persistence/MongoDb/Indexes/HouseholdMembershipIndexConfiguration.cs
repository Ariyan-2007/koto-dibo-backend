using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class HouseholdMembershipIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<HouseholdMembership>(nameof(HouseholdMembership));

        List<CreateIndexModel<HouseholdMembership>> models =
        [
            // History is preserved as multiple documents per (HouseholdId, UserId) over time (one
            // per stint), so the uniqueness constraint can't cover the whole collection — only an
            // ACTIVE stint must be unique per household/user. A partial index enforces exactly
            // that at the database level, closing the same kind of concurrent-add race the Users
            // collection's email index closes for registration.
            new(Builders<HouseholdMembership>.IndexKeys.Ascending(m => m.HouseholdId).Ascending(m => m.UserId),
                new CreateIndexOptions<HouseholdMembership>
                {
                    Unique = true,
                    Name = "ux_householdmembership_household_user_active",
                    PartialFilterExpression = Builders<HouseholdMembership>.Filter.Eq(m => m.Status, HouseholdMembershipStatus.Active),
                }),

            // "Which households does this user belong to" (GetMyHouseholdsAsync).
            new(Builders<HouseholdMembership>.IndexKeys.Ascending(m => m.UserId).Ascending(m => m.Status),
                new CreateIndexOptions { Name = "ix_householdmembership_userid_status" }),

            // "Who belongs to this household" (GetMembersAsync, access checks).
            new(Builders<HouseholdMembership>.IndexKeys.Ascending(m => m.HouseholdId).Ascending(m => m.Status),
                new CreateIndexOptions { Name = "ix_householdmembership_householdid_status" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
