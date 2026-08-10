using KotoDibo.Domain.Entities;
using KotoDibo.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Configurations;

public class UserCredentialConfiguration : IMongoClassMapConfiguration
{
    public void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(UserCredential)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<UserCredential>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new StringSerializer(BsonType.ObjectId))
                .SetIdGenerator(StringObjectIdGenerator.Instance);
            cm.GetMemberMap(x => x.Provider)
                .SetSerializer(new EnumSerializer<AuthProvider>(BsonType.String));
        });
    }
}
