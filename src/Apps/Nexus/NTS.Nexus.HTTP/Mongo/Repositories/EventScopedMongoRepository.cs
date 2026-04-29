using System.Linq.Expressions;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public abstract class EventScopedMongoRepository<T> : MongoRepository<T>, IEventResetRepository
    where T : class, IEventScopedDocument
{
    protected EventScopedMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection) { }

    protected override Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }

    public virtual Task DeleteAllForEvent(int eventId)
    {
        return Delete(x => x.EventId == eventId);
    }
}
