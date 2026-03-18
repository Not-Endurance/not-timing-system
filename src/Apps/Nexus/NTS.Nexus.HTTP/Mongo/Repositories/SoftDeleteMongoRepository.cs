using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Storage.Mongo;
using Not.Structures;
using NTS.Application.Shared;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public abstract class SoftDeleteMongoRepository<T> : MongoRepository<T>, ISoftDelete<T>
    where T : class, IIdentifiable, ISoftDeletableDocument
{
    protected SoftDeleteMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection) { }

    protected virtual FilterDefinition<T> IsNotDeletedFilter()
    {
        return Builders<T>.Filter.Where(x => x.IsDeleted != true);
    }

    protected virtual FilterDefinition<T> CombineWithIsNotDeleted(Expression<Func<T, bool>> filter)
    {
        return Builders<T>.Filter.And(Builders<T>.Filter.Where(filter), IsNotDeletedFilter());
    }

    protected virtual FilterDefinition<T> CombineWithIsNotDeleted(FilterDefinition<T> filter)
    {
        return Builders<T>.Filter.And(filter, IsNotDeletedFilter());
    }

    protected virtual UpdateDefinition<T> SoftDeleteUpdateDefinition(int? deletedVersion = null)
    {
        return Builders<T>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedVersion, deletedVersion);
    }

    protected virtual void PrepareForCreate(T item)
    {
        item.IsDeleted = false;
        item.DeletedVersion = null;
    }

    public override async Task Create(T item)
    {
        PrepareForCreate(item);
        await base.Create(item);
    }

    public override async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return await GetCollection().Find(CombineWithIsNotDeleted(filter)).FirstOrDefaultAsync();
    }

    public override async Task<T?> Read(int id)
    {
        var getById = Builders<T>.Filter.Where(GetIdFilter(id));
        return await GetCollection().Find(CombineWithIsNotDeleted(getById)).FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<T>> ReadMany()
    {
        return await GetCollection().Find(IsNotDeletedFilter()).ToListAsync();
    }

    public override async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        return await GetCollection().Find(CombineWithIsNotDeleted(filter)).ToListAsync();
    }

    public override async Task Update(T item)
    {
        var getItemFilter = Builders<T>.Filter.Where(GetItemFilter(item));
        await GetCollection().UpdateOneAsync(CombineWithIsNotDeleted(getItemFilter), GetUpdateDefinition(item));
    }

    public override async Task Delete(int id)
    {
        var getById = Builders<T>.Filter.Where(GetIdFilter(id));
        await GetCollection().UpdateManyAsync(CombineWithIsNotDeleted(getById), SoftDeleteUpdateDefinition());
    }

    public override async Task Delete(T item)
    {
        await SoftDelete(item);
    }

    public override async Task Delete(Expression<Func<T, bool>> filter)
    {
        await SoftDelete(filter);
    }

    public override async Task Delete(IEnumerable<T> items)
    {
        await SoftDelete(items);
    }

    public virtual async Task SoftDelete(T item)
    {
        var getItemFilter = Builders<T>.Filter.Where(GetItemFilter(item));
        await GetCollection().UpdateManyAsync(CombineWithIsNotDeleted(getItemFilter), SoftDeleteUpdateDefinition());
    }

    public virtual async Task SoftDelete(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await SoftDelete(item);
        }
    }

    public virtual async Task SoftDelete(Expression<Func<T, bool>> filter)
    {
        await GetCollection().UpdateManyAsync(CombineWithIsNotDeleted(filter), SoftDeleteUpdateDefinition());
    }

    public virtual async Task HardDelete(int id)
    {
        var filter = Builders<T>.Filter.Where(GetIdFilter(id));
        await GetCollection().DeleteOneAsync(CombineWithIsNotDeleted(filter));
    }

    public virtual async Task HardDelete(T item)
    {
        var filter = Builders<T>.Filter.Where(GetItemFilter(item));
        await GetCollection().DeleteOneAsync(CombineWithIsNotDeleted(filter));
    }

    public virtual async Task HardDelete(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await HardDelete(item);
        }
    }

    public virtual async Task HardDelete(Expression<Func<T, bool>> filter)
    {
        await GetCollection().DeleteManyAsync(CombineWithIsNotDeleted(filter));
    }
}
