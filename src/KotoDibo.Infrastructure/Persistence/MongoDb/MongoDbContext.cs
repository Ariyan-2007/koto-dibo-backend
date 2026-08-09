using KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        MongoClassMapRegistrar.RegisterAll();

        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string collectionName) => _database.GetCollection<T>(collectionName);
}
