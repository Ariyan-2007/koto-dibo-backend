using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public class BudgetConfiguration : IMongoClassMapConfiguration
{
    public void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Budget)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<Budget>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
            cm.GetMemberMap(x => x.PeriodType)
                .SetSerializer(new EnumSerializer<BudgetPeriodType>(BsonType.String));
            cm.GetMemberMap(x => x.Status)
                .SetSerializer(new EnumSerializer<BudgetStatus>(BsonType.String));
        });
    }
}
