using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class UserCredentialIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<UserCredential>(nameof(UserCredential));

        var model = new CreateIndexModel<UserCredential>(
            Builders<UserCredential>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.Provider),
            new CreateIndexOptions { Unique = true, Name = "ux_usercredential_userid_provider" });

        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
