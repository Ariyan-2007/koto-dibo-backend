namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public interface IMongoIndexConfiguration
{
    Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken);
}
