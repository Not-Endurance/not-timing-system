using Not.Application.CRUD.Ports;
using NTS.Contracts.Access.Models;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Core.Events;

public interface IEventInformationRepository : IRepository<EventInformation>
{
    Task<IEnumerable<EventInformation>> ReadLive();
    Task<IEnumerable<EventInformation>> ReadHistoric();

    /// <summary>What the person may do about the Event, as the Api says for them: its stage, its level and its actions.</summary>
    Task<EventCapabilitiesModel> CapabilitiesOf(Guid eventId);

    /// <summary>Removes the Event with what it made and what was kept for it, as the Api does for a Console Operator's reset.</summary>
    Task Remove(Guid eventId);

    /// <summary>
    /// Starts the Event from its Setup: the Event is the Core document made from the Setup, with the copies it keeps and
    /// the Participations and Rankings it creates (ADR-0006). The Console Operator's, before the Event starts.
    /// </summary>
    Task<EventInformation> Start(Guid configureEventId);

    /// <summary>
    /// Resets the currently selected Event to its Setup: the Console Operator's, while the Event is Live.
    /// </summary>
    /// <remarks>
    /// This removes the Event together with the documents it made and the ones that were kept for it. After a successful
    /// reset the Event no longer appears in the reads of the Live Events, which also affects Home/startup behavior that
    /// relies on the list of Live Events.
    /// </remarks>
    Task Reset();
}
