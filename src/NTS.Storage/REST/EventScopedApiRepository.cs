using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Storage.REST;
using NTS.Application.Socket;

namespace NTS.Storage.REST;

public abstract class EventScopedApiRepository<T, TModel> : RestApiRepository<T, TModel>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    readonly INtsSocketContext _socketContext;

    protected EventScopedApiRepository(string endpoint, NHttpClient client, INtsSocketContext socketContext)
        : base(endpoint, client)
    {
        _socketContext = socketContext;
    }

    protected override string ResolveEndpoint()
    {
        var eventId = _socketContext.Event?.Id;
        GuardHelper.ThrowIfDefault(eventId, "Cannot use event-scoped repository before selecting an event.");

        return $"events/{eventId.Value}/{Endpoint}";
    }
}
