using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using NTS.Application.Models;

namespace NTS.Nexus.HTTP.Mongo;

public abstract class MongoRepository<T> : IRepository<T>
    where T : IDocument
{
    readonly IMongoContext _context;
    readonly string _db;
    readonly string _collection;

    public MongoRepository(IMongoContext context, string db, string collection)
    {
        _context = context;
        _db = db;
        _collection = collection;
    }

    protected abstract UpdateDefinition<T> GetUpdateDefinition(T document);

    protected IMongoCollection<T> GetCollection()
    {
        return _context.Client.GetDatabase(_db).GetCollection<T>(_collection);
    }

    public async Task Create(T item)
    {
        try
        {
            if (item.Id == default)
            {
                throw new ApplicationException($"Invalid ID '{item.Id}'");
            }
            await GetCollection().InsertOneAsync(item);
        }
        catch (MongoWriteException ex)
        {
            if (ex.WriteError.Code == 11000)
            {
                throw new ApplicationException($"Could not insert. Document with ID '{item.Id}' already exists", ex);
            }
            else
            {
                throw;
            }
        }
    }

    public async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return await GetCollection().Find(filter).FirstOrDefaultAsync();
    }

    public async Task<T?> Read(int id)
    {
        return await Read(x => x.Id == id);
    }

    public async Task<IEnumerable<T>> ReadAll()
    {
        return await ReadAll(x => true);
    }

    public async Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter)
    {
        return await GetCollection().Find(filter).ToListAsync();
    }

    public async Task Update(T items)
    {
        var updateDefinition = GetUpdateDefinition(items);
        await GetCollection().UpdateOneAsync(x => x.Id == items.Id, updateDefinition);
    }

    public async Task Delete(int id)
    {
        await GetCollection().DeleteOneAsync(x => x.Id == id);
    }

    public Task Delete(T item)
    {
        return Delete(item.Id);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        await GetCollection().DeleteManyAsync(filter);
    }

    public Task Delete(IEnumerable<T> items)
    {
        throw new NotImplementedException(
            "Batch delete with full entities isn't supported. Probably remove this method"
        );
    }
}
