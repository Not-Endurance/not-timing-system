using Microsoft.Extensions.DependencyInjection;
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
    readonly IServiceProvider _serviceProvider;

    protected EventScopedApiRepository(string endpoint, NHttpClient client, IServiceProvider serviceProvider)
        : base(endpoint, client)
    {
        _serviceProvider = serviceProvider;
    }

    protected override string ResolveEndpoint()
    {
        var eventId = _serviceProvider.GetService<INtsSocketContext>()?.Event?.Id;
        GuardHelper.ThrowIfDefault(eventId, "Cannot use event-scoped repository before selecting an event.");

        return $"events/{eventId.Value}/{Endpoint}";
    }
}
