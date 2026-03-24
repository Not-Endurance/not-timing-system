using Not.Application.CRUD.Ports;
using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Core;

public interface IEnduranceEventRepository : IRepository<EnduranceEvent>
{
    Task<EnduranceEvent> Start(int upcomingEventId);

    /// <summary>
    /// Soft-resets the currently selected endurance event in Nexus.
    /// </summary>
    /// <remarks>
    /// This call resets the active event root together with the event-scoped Core documents that belong to it.
    /// After a successful reset the event no longer appears in the normal active-event reads, which also affects
    /// Home/startup behavior that relies on the active event list.
    /// </remarks>
    Task Reset();
}
