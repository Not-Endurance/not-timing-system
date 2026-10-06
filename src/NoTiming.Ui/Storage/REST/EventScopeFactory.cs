using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.REST;

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

    Guid ResolveEventId()
    {
        var eventId = _socketContext.Event?.Id;
        GuardHelper.ThrowIfDefault(eventId, "Cannot use event-scoped repository before selecting an event.");
        return eventId.Value;
    }
}

public class EventRepositoryScope<T> : IRepositoryScope<T>
    where T : IEventScoped
{
    public EventRepositoryScope(Guid eventId)
    {
        Filter = document => document.EventId == eventId;
    }

    public Expression<Func<T, bool>> Filter { get; }
}
