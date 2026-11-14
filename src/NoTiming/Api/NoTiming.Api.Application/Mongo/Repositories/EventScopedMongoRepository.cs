using System.Linq.Expressions;
using MongoDB.Driver;
using NC.Domain.Exceptions;
using NC.Storage.Mongo;
using NC.Structures;
using NoTiming.Application.Contracts.Core.Models;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Api.Application.Mongo.Repositories;

public abstract class EventScopedMongoRepository<T> : MongoRepository<T>, IEventResetRepository
    where T : class, IEventScoped, IIdentifiable
{
    readonly IMongoContext _context;
    readonly string _db;

    protected EventScopedMongoRepository(IMongoContext context, string db, string collection)
        : base(context, db, collection)
    {
        _context = context;
        _db = db;
    }

    protected override Expression<Func<T, bool>> GetItemFilter(T item)
    {
        return x => x.Id == item.Id && x.EventId == item.EventId;
    }

    // Once an event is finalized its participations, phases, handouts and rankings are locked. An admin reopens it.
    public override async Task Create(T item)
    {
        await EnsureEventNotFinalized(item.EventId);
        await base.Create(item);
    }

    public override async Task Update(T items)
    {
        await EnsureEventNotFinalized(items.EventId);
        await base.Update(items);
    }

    public override async Task Delete(T item)
    {
        await EnsureEventNotFinalized(item.EventId);
        await base.Delete(item);
    }

    public virtual async Task DeleteAllForEvent(int eventId)
    {
        await EnsureEventNotFinalized(eventId);
        await DeleteMany(x => x.EventId == eventId);
    }

    async Task EnsureEventNotFinalized(int eventId)
    {
        var events = _context
            .Client.GetDatabase(_db)
            .GetCollection<EventInformationModel>(MongoConstants.EVENT_INFORMATION_COLLECTION);
        var isFinalized = await events.Find(x => x.Id == eventId && x.IsFinalized).AnyAsync();
        if (isFinalized)
        {
            throw new DomainException(
                $"Cannot change event '{eventId}' because it is finalized. An admin can reopen the event."
            );
        }
    }
}
