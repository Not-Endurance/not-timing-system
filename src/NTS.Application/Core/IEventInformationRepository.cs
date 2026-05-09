using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Core;

public interface IEventInformationRepository : IRepository<EventInformation>
{
    Task<IEnumerable<EventInformation>> ReadActive();
    Task<IEnumerable<EventInformation>> ReadPast();
    Task<EventInformation> Start(int configureEventId);
    Task Deactivate();

    /// <summary>
    /// Permanently resets the currently selected event information in Nexus.
    /// </summary>
    /// <remarks>
    /// This call deletes the active event root together with the event-scoped Core documents that belong to it.
    /// After a successful reset the event no longer appears in the normal active-event reads, which also affects
    /// Home/startup behavior that relies on the active event list.
    /// </remarks>
    Task Reset();
}
