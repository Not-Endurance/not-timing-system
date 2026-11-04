using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.OData.Query;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Not.Structures;

namespace Not.Storage.Mongo;

public abstract class MongoRepository<T> : IMongoRepository<T>
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

    protected virtual Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id;
    }

    protected IMongoCollection<T> GetCollection()
    {
        return _context.Client.GetDatabase(_db).GetCollection<T>(_collection);
    }

    protected virtual IQueryable<T> ApplyODataOptions(IQueryable<T> query, ODataQueryOptions<T> options)
    {
        return (IQueryable<T>)options.ApplyTo(query, new ODataQuerySettings());
    }

    public virtual async Task Create(T item)
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

    public virtual async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(Read));
        return await GetCollection().Find(filter).FirstOrDefaultAsync();
    }

    public virtual async Task<T?> Read(int id)
    {
        using var activity = StartActivity(nameof(Read));
        return await GetCollection().Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        using var activity = StartActivity(nameof(ReadMany));
        return await GetCollection().Find(x => true).ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(ReadMany));
        return await GetCollection().Find(filter).ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> ReadMany(ODataQueryOptions<T> options)
    {
        using var activity = StartActivity(nameof(ReadMany));
        return await ApplyODataOptions(GetCollection().AsQueryable(), options).ToListAsync();
    }

    public virtual async Task Update(T items)
    {
        using var activity = StartActivity(nameof(Update));

        var updateDefinition = GetUpdateDefinition(items);
        var filter = GetItemFilter(items);
        await GetCollection().UpdateOneAsync(filter, updateDefinition);
    }

    public virtual async Task Delete(int id)
    {
        using var activity = StartActivity(nameof(DeleteMany));
        await GetCollection().DeleteOneAsync(x => x.Id == id);
    }

    public virtual async Task Delete(T item)
    {
        using var activity = StartActivity(nameof(DeleteMany));
        var filter = GetItemFilter(item);
        await GetCollection().DeleteOneAsync(filter);
    }

    public virtual async Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        using var activity = StartActivity(nameof(DeleteMany));
        await GetCollection().DeleteManyAsync(filter);
    }

    public virtual async Task DeleteMany(IEnumerable<T> items)
    {
        using var activity = StartActivity(nameof(DeleteMany));

        var filters = items.Select(item => Builders<T>.Filter.Where(GetItemFilter(item))).ToArray();
        if (filters.Length == 0)
        {
            return;
        }

        var filter = filters.Length == 1 ? filters[0] : Builders<T>.Filter.Or(filters);
        await GetCollection().DeleteManyAsync(filter);
    }

    Activity? StartActivity(string methodName)
    {
        var name = $"{GetType().Name}.{methodName}";
        return SOURCE.StartActivity(name, ActivityKind.Internal);
    }
}
