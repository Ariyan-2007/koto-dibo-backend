using KotoDibo.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public class UtilityTariffConfigConfiguration : IMongoClassMapConfiguration
{
    public void Configure()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TariffBand)))
        {
            BsonClassMap.RegisterClassMap<TariffBand>(cm => cm.AutoMap());
        }

        if (BsonClassMap.IsClassMapRegistered(typeof(UtilityTariffConfig)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<UtilityTariffConfig>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
        });
    }
}
