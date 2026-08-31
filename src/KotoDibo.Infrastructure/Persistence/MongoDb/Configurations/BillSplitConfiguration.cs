using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public class BillSplitConfiguration : IMongoClassMapConfiguration
{
    public void Configure()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(BillSplitMemberInput)))
        {
            BsonClassMap.RegisterClassMap<BillSplitMemberInput>(cm => cm.AutoMap());
        }

        if (BsonClassMap.IsClassMapRegistered(typeof(BillSplit)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<BillSplit>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
            cm.GetMemberMap(x => x.SplitMethod)
                .SetSerializer(new EnumSerializer<BillSplitMethod>(BsonType.String));
            cm.GetMemberMap(x => x.Status)
                .SetSerializer(new EnumSerializer<FinancialEntryStatus>(BsonType.String));
        });
    }
}
