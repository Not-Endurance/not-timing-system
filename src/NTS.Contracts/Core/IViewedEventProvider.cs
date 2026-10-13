using Not.Events;

namespace NTS.Contracts.Core;

/// <summary>
/// Opens the Event of a route for the Core views (#630, ADR-0007). The mode follows from the Event: a Live Event gets the
/// live view and a Historic Event the historic one, and opening a Historic Event never disturbs the connection to a Live
/// Event. An Event that ends while it is open is told by a refused write (#631), and it is opened again, as a Historic one.
/// </summary>
public interface IViewedEventProvider
{
    /// <summary>
    /// Raised, with the id of the Event, once for each view of a Live Event that was told by a refused write that the Event has
    /// ended: whoever shows that view opens the Event again, and it is Historic then (ADR-0007: no end notification is added to
    /// the wire, so this is how the app learns it).
    /// </summary>
    IEventSubscriber<Guid> EventEnded { get; }

    /// <summary>The Event as the Api lists it, Live or Historic, or none when there is no such Event.</summary>
    Task<IViewedEvent?> Open(Guid eventId);
}
