using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Application.Shared;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public abstract class EventScopedMongoRepository<T> : SoftDeleteMongoRepository<T>, IEventResetRepository
    where T : class, IEventScopedDocument, IDocument, ISoftDeletableDocument
{
    protected EventScopedMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection) { }

    protected override Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }

    public async Task<int?> GetMaxDeletedVersion(int eventId)
    {
        var deleted = await GetCollection()
            .Find(x => x.EventId == eventId && x.IsDeleted)
            .SortByDescending(x => x.DeletedVersion)
            .FirstOrDefaultAsync();

        return deleted?.DeletedVersion;
    }

    public async Task SoftDelete(int eventId, int deletedVersion)
    {
        var filter = Builders<T>.Filter.Where(x => x.EventId == eventId && x.IsDeleted != true);
        await GetCollection().UpdateManyAsync(filter, SoftDeleteUpdateDefinition(deletedVersion));
    }
}
