using System.Linq.Expressions;
using Not.Storage.Mongo;
using NTS.Application.Shared;

namespace NTS.Nexus.HTTP.Mongo.Repositories;

public abstract class EventScopedMongoRepository<T> : MongoRepository<T>
    where T : class, IEventScopedDocument
{
    protected EventScopedMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection) { }

    protected override Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }
}
