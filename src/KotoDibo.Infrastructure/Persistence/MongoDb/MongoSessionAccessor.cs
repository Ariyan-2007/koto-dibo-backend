using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb;

// Ambient handle to the current Mongo transaction session, if one is active. MongoUnitOfWork sets
// this for the duration of its callback; MongoRepository reads it so every repository call made
// from inside a unit-of-work action is automatically enlisted in the same transaction, without
// threading a session parameter through every service method.
public interface IMongoSessionAccessor
{
    IClientSessionHandle? Session { get; set; }
}

// AsyncLocal, not a plain field: a scoped/singleton field would leak one session across concurrent
// requests. AsyncLocal flows correctly through the awaited callback in MongoUnitOfWork.ExecuteAsync
// (same logical async call chain) while staying isolated per request.
public class MongoSessionAccessor : IMongoSessionAccessor
{
    private static readonly AsyncLocal<IClientSessionHandle?> Current = new();

    public IClientSessionHandle? Session
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
