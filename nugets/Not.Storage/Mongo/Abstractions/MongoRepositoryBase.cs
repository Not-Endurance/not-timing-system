using MongoDB.Driver;

namespace Not.Storage.Mongo.Abstractions;

public abstract class MongoRepositoryBase<T>
{
    readonly IMongoContext _context;
    readonly string _db;
    readonly string _collection;

    protected MongoRepositoryBase(IMongoContext context, string db, string collection)
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
}
