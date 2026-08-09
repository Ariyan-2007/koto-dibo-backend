using KotoDibo.Application.Common.Interfaces;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Repositories;

public class MongoRepository<T> : IRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<T>(typeof(T).Name);
    }

    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
