using System.Linq.Expressions;
using Not.Storage.Mongo;
using Not.Structures;
using NTS.Domain.Core.Aggregates;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public abstract class EventScopedMongoRepository<T> : MongoRepository<T>, IEventResetRepository
    where T : class, IEventScoped, IIdentifiable
{
    protected EventScopedMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection) { }

    protected override Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }

    public virtual Task DeleteAllForEvent(int eventId)
    {
        return DeleteMany(x => x.EventId == eventId);
    }
}
