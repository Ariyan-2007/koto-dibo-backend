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

        Client = new MongoClient(settings.Value.ConnectionString);
        _database = Client.GetDatabase(settings.Value.DatabaseName);
    }

    // Exposed so MongoUnitOfWork can start multi-document transaction sessions. Atlas (this
    // project's only supported deployment target — see appsettings) is always backed by a replica
    // set, so transactions are available without any extra server configuration.
    public IMongoClient Client { get; }

    public IMongoCollection<T> GetCollection<T>(string collectionName) => _database.GetCollection<T>(collectionName);
}
