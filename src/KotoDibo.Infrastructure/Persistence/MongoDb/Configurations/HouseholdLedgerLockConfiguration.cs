using KotoDibo.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public class HouseholdLedgerLockConfiguration : IMongoClassMapConfiguration
{
    public void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(HouseholdLedgerLock)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<HouseholdLedgerLock>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            // Id is the HouseholdId (already an ObjectId string) — assigned by the caller, never generated.
            cm.MapIdProperty(x => x.Id).SetSerializer(new StringSerializer(BsonType.ObjectId));
        });
    }
}
