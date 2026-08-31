using KotoDibo.Domain.Entities;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Indexes;

public class UtilityTariffConfigIndexConfiguration : IMongoIndexConfiguration
{
    public async Task ConfigureIndexesAsync(MongoDbContext context, CancellationToken cancellationToken)
    {
        var collection = context.GetCollection<UtilityTariffConfig>(nameof(UtilityTariffConfig));

        List<CreateIndexModel<UtilityTariffConfig>> models =
        [
            // BillSplitService's tariff lookup: active config for a country (+ optional provider).
            new(Builders<UtilityTariffConfig>.IndexKeys.Ascending(t => t.Country).Ascending(t => t.Provider).Ascending(t => t.IsActive),
                new CreateIndexOptions { Name = "ix_utilitytariffconfig_country_provider_isactive" }),
        ];

        await collection.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
