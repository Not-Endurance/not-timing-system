using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Storage.REST;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class EventInformationApiRepository
    : ApiRepository<EventInformation, EventInformationModel>,
        IEventInformationRepository
{
    readonly INtsSocketContext _socketContext;

    public EventInformationApiRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("event-information", client)
    {
        _socketContext = socketContext;
    }

    public async Task<IEnumerable<EventInformation>> ReadActive()
    {
        var models = await HandleRequest(Client.Get<IEnumerable<EventInformationModel>>($"{Endpoint}/active")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }

    public async Task<IEnumerable<EventInformation>> ReadPast()
    {
        var models = await HandleRequest(Client.Get<IEnumerable<EventInformationModel>>($"{Endpoint}/past")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }

    public async Task<EventInformation> Start(int configureEventId)
    {
        var result = await Client.Post<EventInformationModel>(
            $"{Endpoint}/{configureEventId}/start",
            new StartEventInformationRequest()
        );
        if (!result.IsSuccess)
        {
            throw new DomainException(string.Join(Environment.NewLine, result.Errors));
        }

        var eventInformation = result.Data?.MapToEntity();
        return eventInformation ?? throw GuardHelper.Exception("Event information start returned no event payload.");
    }

    public async Task Deactivate()
    {
        var eventId = _socketContext.Event?.Id;
        if (eventId == null)
        {
            return;
        }

        await Client.Post<Result.Empty>(
            $"{Endpoint}/{eventId.Value}/deactivate",
            new DeactivateEventInformationRequest()
        );
    }

    /// <summary>
    /// Permanently resets the currently selected event information in Nexus.
    /// </summary>
    /// <remarks>
    /// This deletes the active event root together with its event-scoped Core child documents, which removes the
    /// event from the active-event reads used by Home and startup reconnect logic.
    /// </remarks>
    public async Task Reset()
    {
        var eventId = _socketContext.Event?.Id;
        if (eventId == null)
        {
            return;
        }

        await Client.Delete($"{Endpoint}/{eventId.Value}/reset");
    }

    sealed class StartEventInformationRequest { }

    sealed class DeactivateEventInformationRequest { }
}
