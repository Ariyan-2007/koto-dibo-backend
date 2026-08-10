using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class UserIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<User>(nameof(User));

        var model = new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Unique = true, Name = "ux_user_normalizedemail" });

        await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
