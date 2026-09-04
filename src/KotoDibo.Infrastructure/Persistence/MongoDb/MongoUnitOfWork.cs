using KotoDibo.Application.Common.Interfaces;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb;

public class MongoUnitOfWork : IUnitOfWork
{
    private readonly MongoDbContext _context;
    private readonly IMongoSessionAccessor _sessionAccessor;

    public MongoUnitOfWork(MongoDbContext context, IMongoSessionAccessor sessionAccessor)
    {
        _context = context;
        _sessionAccessor = sessionAccessor;
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        // Nested unit-of-work calls (e.g. a service method invoking another service method that
        // also wraps itself) reuse the outer transaction instead of starting a second, conflicting
        // one — Mongo sessions aren't reentrant.
        if (_sessionAccessor.Session is not null)
        {
            return await action(cancellationToken);
        }

        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        _sessionAccessor.Session = session;
        try
        {
            return await session.WithTransactionAsync(
                (_, ct) => action(ct),
                cancellationToken: cancellationToken);
        }
        finally
        {
            _sessionAccessor.Session = null;
        }
    }
}
