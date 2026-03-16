using System.Diagnostics;
using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Structures;

namespace Not.Storage.Mongo;

public abstract class MongoRepository<T> : IRepository<T>
    where T : IIdentifiable
{
    static readonly ActivitySource SOURCE = new("Not.Storage.Mongo");

    readonly IMongoContext _context;
    readonly string _db;
    readonly string _collection;

    // TODO: Pass telemetry service here
    public MongoRepository(IMongoContext context, string db, string collection)
    {
        _context = context;
        _db = db;
        _collection = collection;
    }

    protected abstract UpdateDefinition<T> GetUpdateDefinition(T document);

    protected virtual Expression<Func<T, bool>> GetIdFilter(int id)
    {
        return x => x.Id == id;
    }

    protected virtual Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id;
    }

    protected IMongoCollection<T> GetCollection()
    {
        return _context.Client.GetDatabase(_db).GetCollection<T>(_collection);
    }

    public async Task Create(T item)
    {
        using var activity = StartActivity(nameof(Create));

        try
        {
            await GetCollection().InsertOneAsync(item);
        }
        catch (MongoWriteException ex)
        {
            if (ex.WriteError.Code == 11000)
            {
                throw new ApplicationException($"Could not insert. Document with ID '{item.Id}' already exists", ex);
            }

            throw;
        }
    }

    public async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(Read));
        return await GetCollection().Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T?> Read(int id)
    {
        using var activity = StartActivity(nameof(Read));
        return await GetCollection().Find(GetIdFilter(id)).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<T>> ReadMany()
    {
        using var activity = StartActivity(nameof(ReadMany));
        return await GetCollection().Find(x => true).ToListAsync();
    }

    public async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(ReadMany));
        return await GetCollection().Find(filter).ToListAsync();
    }

    public async Task Update(T items)
    {
        using var activity = StartActivity(nameof(Update));

        var updateDefinition = GetUpdateDefinition(items);
        await GetCollection().UpdateOneAsync(GetItemFilter(items), updateDefinition);
    }

    public async Task Delete(int id)
    {
        using var activity = StartActivity(nameof(Delete));
        await GetCollection().DeleteOneAsync(GetIdFilter(id));
    }

    public async Task Delete(T item)
    {
        using var activity = StartActivity(nameof(Delete));
        await GetCollection().DeleteOneAsync(GetItemFilter(item));
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(Delete));
        await GetCollection().DeleteManyAsync(filter);
    }

    public Task Delete(IEnumerable<T> items)
    {
        using var activity = StartActivity(nameof(Delete));

        throw new NotImplementedException(
            "Batch delete with full entities isn't supported. Probably remove this method"
        );
    }

    Activity? StartActivity(string methodName)
    {
        return SOURCE.StartActivity($"{GetType().Name}.{methodName}", ActivityKind.Internal);
    }
}
