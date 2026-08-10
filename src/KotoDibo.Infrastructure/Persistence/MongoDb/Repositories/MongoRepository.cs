using System.Linq.Expressions;
using System.Reflection;
using KotoDibo.Application.Common.Interfaces;
using DuplicateKeyException = KotoDibo.Application.Common.Exceptions.DuplicateKeyException;
using MongoDB.Driver;

namespace KotoDibo.Infrastructure.Persistence.MongoDb.Repositories;

public class MongoRepository<T> : IRepository<T> where T : class
{
    private static readonly PropertyInfo IdProperty = typeof(T).GetProperty("Id")
        ?? throw new InvalidOperationException($"Entity '{typeof(T).Name}' must have an 'Id' property to be used with {nameof(MongoRepository<T>)}.");

    // Builders<T>.Filter.Eq("_id", id) (string field name) renders a literal string filter and
    // does NOT route through the Id member's custom serializer (StringSerializer(BsonType.ObjectId)),
    // so it never matches a stored ObjectId. An expression-based selector does resolve through the
    // class map correctly; this builds the equivalent of `x => x.Id` at runtime since T isn't
    // statically known to have an Id property.
    private static readonly Expression<Func<T, string>> IdSelector = BuildIdSelector();

    private readonly IMongoCollection<T> _collection;

    public MongoRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<T>(typeof(T).Name);
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.Find(Builders<T>.Filter.Eq(IdSelector, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _collection.Find(predicate).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _collection.Find(predicate).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(FilterDefinition<T>.Empty).ToListAsync(cancellationToken);

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.InsertOneAsync(entity, options: null, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateKeyException($"A '{typeof(T).Name}' with a conflicting unique value already exists.");
        }

        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var id = (string)IdProperty.GetValue(entity)!;
        await _collection.ReplaceOneAsync(Builders<T>.Filter.Eq(IdSelector, id), entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.DeleteOneAsync(Builders<T>.Filter.Eq(IdSelector, id), cancellationToken);

    private static Expression<Func<T, string>> BuildIdSelector()
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, IdProperty);
        return Expression.Lambda<Func<T, string>>(propertyAccess, parameter);
    }
}
