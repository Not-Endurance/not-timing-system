using System.Linq.Expressions;
using NC.Application.CRUD.Ports;
using NC.Application.HTTP;
using NC.Domain.Abstractions;
using NC.Exceptions;
using NC.Krud.Abstractions;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Aggregates;

namespace NoTiming.Storage.REST;

public abstract class EventScopedApiRepository<T, TModel> : ApiRepository<T, TModel>, IEventScopedRepository<T>
    where T : class, IEntity, IEventScoped
    where TModel : class, IEventScoped, IKrudModel<T>, new()
{
    protected EventScopedApiRepository(string endpoint, NHttpClient client, EventScopeFactory<T> eventScopeFactory)
        : base(endpoint, client, eventScopeFactory) { }
}

public class EventScopeFactory<T> : IRepositoryScopeFactory<T>
    where T : IEventScoped
{
    readonly INoTimingSocketContext _socketContext;

    public EventScopeFactory(INoTimingSocketContext socketContext)
    {
        _socketContext = socketContext;
    }

    public IRepositoryScope<T> Create()
    {
        var eventId = ResolveEventId();
        return new EventRepositoryScope<T>(eventId);
    }

    int ResolveEventId()
    {
        var eventId = _socketContext.Event?.Id;
        GuardHelper.ThrowIfDefault(eventId, "Cannot use event-scoped repository before selecting an event.");
        return eventId.Value;
    }
}

public class EventRepositoryScope<T> : IRepositoryScope<T>
    where T : IEventScoped
{
    public EventRepositoryScope(int eventId)
    {
        Filter = document => document.EventId == eventId;
    }

    public Expression<Func<T, bool>> Filter { get; }
}
