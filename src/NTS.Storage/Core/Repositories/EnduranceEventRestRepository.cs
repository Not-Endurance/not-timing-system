using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class EnduranceEventRestRepository : RestApiRepository<EnduranceEvent, EnduranceEventModel>, IEnduranceEventRepository, ITransient
{
    readonly INtsSocketContext _socketContext;

    public EnduranceEventRestRepository(NHttpClient client, INtsSocketContext socketContext)
        : base("endurance-event", client)
    {
        _socketContext = socketContext;
    }

    /// <summary>
    /// Soft-resets the currently selected endurance event in Nexus.
    /// </summary>
    /// <remarks>
    /// This resets the active event root together with its Core child documents and hides the event from the
    /// normal active-event reads used by Home and startup reconnect logic.
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
}
