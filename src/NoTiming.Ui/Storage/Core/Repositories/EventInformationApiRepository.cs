using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.Core.Repositories;

/// <summary>
/// The Events as the Api serves them (#628): the <c>events</c> resource of ADR-0007 and ADR-0008. The Live Events and the
/// Historic Events are its two named collections, which anybody reads; an Event is started from its Setup, which is the
/// Main Operator's, and what the Api keeps (who runs the Event, the rules the Tenant had when it started) is read and never
/// sent. Whether an Event is Live is the Api's rule and the clock of its host: nothing here stores or asks a flag.
/// </summary>
public class EventInformationApiRepository
    : JsonApiRepository<EventInformation, EventInformationModel>,
        IEventInformationRepository
{
    static readonly string[] NOT_SENT = ["mainOperatorId", "regionalRules", "isDeleted", "deletedVersion"];

    readonly INtsSocketContext _socketContext;

    public EventInformationApiRepository(JsonApiClient client, INtsSocketContext socketContext)
        : base("events", client, NOT_SENT)
    {
        _socketContext = socketContext;
    }

    public async Task<IEnumerable<EventInformation>> ReadLive()
    {
        return await ReadView("live");
    }

    public async Task<IEnumerable<EventInformation>> ReadHistoric()
    {
        return await ReadView("historic", "-endDay");
    }

    /// <summary>
    /// Starts the Event from its Setup: the document names the Setup and no member, because an Event is started and takes
    /// nothing else. What the Api refuses is thrown with what it says, and its code is in
    /// <see cref="JsonApiRepository{T, TModel}.LastError"/>: <c>invalid-setup</c> and <c>incomplete-fei-configuration</c> say
    /// what is missing, <c>not-main-operator</c> that the caller does not run the Event.
    /// </summary>
    public async Task<EventInformation> Start(Guid configureEventId)
    {
        var response = await Client.Send(
            HttpMethod.Post,
            Collection,
            Document(new EventInformationModel { Id = configureEventId }, [])
        );
        if (!response.IsSuccess)
        {
            Failed(response, tell: false);
            throw new DomainException(LastError!.Message);
        }

        return EntityOf(response.Document?.GetProperty("data"))
            ?? throw GuardHelper.Exception("Event start returned no event payload.");
    }

    /// <summary>
    /// Resets the currently selected Event to its Setup: the Main Operator's, while the Event is Live. It removes the Event
    /// together with what it made and what was kept for it, so it no longer appears among the Live Events.
    /// </summary>
    public async Task Reset()
    {
        var eventId = _socketContext.Event?.Id;
        if (eventId == null)
        {
            return;
        }

        await Delete(eventId.Value);
    }
}
