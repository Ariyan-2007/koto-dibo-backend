using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class RefreshTokenIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<RefreshToken>(nameof(RefreshToken));

        List<CreateIndexModel<RefreshToken>> models =
        [
            new(Builders<RefreshToken>.IndexKeys.Ascending(t => t.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_refreshtoken_tokenhash" }),
            new(Builders<RefreshToken>.IndexKeys.Ascending(t => t.UserId),
                new CreateIndexOptions { Name = "ix_refreshtoken_userid" }),
            new(Builders<RefreshToken>.IndexKeys.Ascending(t => t.FamilyId),
                new CreateIndexOptions { Name = "ix_refreshtoken_familyid" }),

            // TTL index: MongoDB's background sweep removes the document once ExpiresAt is in the
            // past, so expired sessions don't accumulate. Session validity itself is still
            // enforced in AuthService (the sweep runs on its own ~60s interval, not instantly).
            new(Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt),
                new CreateIndexOptions { Name = "ttl_refreshtoken_expiresat", ExpireAfter = TimeSpan.Zero }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
