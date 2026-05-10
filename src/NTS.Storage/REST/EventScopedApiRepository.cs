using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Storage.REST;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.REST;

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
    readonly INtsSocketContext _socketContext;

    public EventScopeFactory(INtsSocketContext socketContext)
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
