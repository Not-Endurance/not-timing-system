using Microsoft.AspNetCore.SignalR;
using NTS.Contracts.Live;

namespace NoTiming.Api.Features.Live;

/// <summary>
/// How the Api tells the viewers of an Event that a Participation changed (ADR-0006, ADR-0013). Every path that changes
/// a Participation calls it once per persisted change, after the write has succeeded, so a failed write announces
/// nothing and a viewer never reads again for a change that did not happen.
/// </summary>
public interface IParticipationChanges
{
    /// <summary>
    /// Announces the change to the clients of the Event. It belongs to the write that succeeded, not to the request that
    /// carried it, so it takes no cancellation. It is best effort: when the hub cannot send, that is logged and the
    /// writer is not told, because its write is stored; the viewers read everything again when they reconnect.
    /// </summary>
    Task AnnounceAsync(Guid eventId, Guid participationId);
}

internal sealed class ParticipationChanges : IParticipationChanges
{
    readonly IHubContext<LiveHub, ILiveClientProcedures> _hub;
    readonly ILogger<ParticipationChanges> _logger;

    public ParticipationChanges(IHubContext<LiveHub, ILiveClientProcedures> hub, ILogger<ParticipationChanges> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task AnnounceAsync(Guid eventId, Guid participationId)
    {
        try
        {
            await _hub.Clients.Group(LiveGroup.Name(eventId)).ParticipationChanged(eventId, participationId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The change of Participation {ParticipationId} of Event {EventId} could not be announced.",
                participationId,
                eventId
            );
        }
    }
}
