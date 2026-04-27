using Not.Application.HTTP;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class EnduranceEventRestRepository
    : RestApiRepository<EnduranceEvent, EnduranceEventModel>,
        IEnduranceEventRepository,
        ITransient
{
    readonly INtsSocketContext _socketContext;

    public EnduranceEventRestRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("endurance-event", client)
    {
        _socketContext = socketContext;
    }

    public async Task<IEnumerable<EnduranceEvent>> ReadActive()
    {
        var models = await HandleRequest(Client.Get<IEnumerable<EnduranceEventModel>>($"{Endpoint}/active")) ?? [];
        return models.Select(x => MapEntity(x)!);
    }

    public async Task<EnduranceEvent> Start(int upcomingEventId)
    {
        var result = await Client.Post<EnduranceEventModel>(
            $"{Endpoint}/{upcomingEventId}/start",
            new StartEnduranceEventRequest()
        );
        if (!result.IsSuccess)
        {
            throw new DomainException(string.Join(Environment.NewLine, result.Errors));
        }

        var enduranceEvent = result.Data?.MapToEntity();
        return enduranceEvent ?? throw GuardHelper.Exception("Endurance event start returned no event payload.");
    }

    /// <summary>
    /// Permanently resets the currently selected endurance event in Nexus.
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

    sealed class StartEnduranceEventRequest { }
}
