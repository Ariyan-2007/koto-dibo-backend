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
    private readonly IMongoSessionAccessor _sessionAccessor;

    public MongoRepository(MongoDbContext context, IMongoSessionAccessor sessionAccessor)
    {
        _collection = context.GetCollection<T>(typeof(T).Name);
        _sessionAccessor = sessionAccessor;
    }

    // Every call below routes through the ambient session (if IUnitOfWork.ExecuteAsync has one
    // active for this async flow) so it's automatically enlisted in that transaction; outside a
    // unit-of-work scope, Session is null and behavior is exactly what it was before transactions
    // existed.
    private IClientSessionHandle? Session => _sessionAccessor.Session;

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(IdSelector, id);
        var session = Session;
        return session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var session = Session;
        return session is null
            ? await _collection.Find(predicate).FirstOrDefaultAsync(cancellationToken)
            : await _collection.Find(session, predicate).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var session = Session;
        return session is null
            ? await _collection.Find(predicate).ToListAsync(cancellationToken)
            : await _collection.Find(session, predicate).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var session = Session;
        return session is null
            ? await _collection.Find(FilterDefinition<T>.Empty).ToListAsync(cancellationToken)
            : await _collection.Find(session, FilterDefinition<T>.Empty).ToListAsync(cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = Session;
            if (session is null)
            {
                await _collection.InsertOneAsync(entity, options: null, cancellationToken);
            }
            else
            {
                await _collection.InsertOneAsync(session, entity, options: null, cancellationToken);
            }
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
        var filter = Builders<T>.Filter.Eq(IdSelector, id);
        var session = Session;
        if (session is null)
        {
            await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        }
        else
        {
            await _collection.ReplaceOneAsync(session, filter, entity, cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(IdSelector, id);
        var session = Session;
        if (session is null)
        {
            await _collection.DeleteOneAsync(filter, cancellationToken);
        }
        else
        {
            await _collection.DeleteOneAsync(session, filter, cancellationToken: cancellationToken);
        }
    }

    private static Expression<Func<T, string>> BuildIdSelector()
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, IdProperty);
        return Expression.Lambda<Func<T, string>>(propertyAccess, parameter);
    }
}
