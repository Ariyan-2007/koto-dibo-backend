using KotoDibo.Application.Common.Interfaces;

namespace KotoDibo.UnitTests.TestHelpers;

// Real transactional behavior (IMongoSessionAccessor-based enlistment, actual commit/rollback) is
// Mongo-driver-specific and lives in MongoUnitOfWork — not something a mocked IRepository<T> can
// meaningfully exercise. For unit tests, the action just needs to run as if it were transactional
// (i.e. run at all, synchronously in sequence); this stands in for that without a real database.
public class PassthroughUnitOfWork : IUnitOfWork
{
    public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
        => action(cancellationToken);
}
