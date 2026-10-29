namespace NTS.Contracts.Core;

/// <summary>
/// Opens the Event of a route for the Core views (#630, ADR-0007). The mode follows from the Event: a Live Event gets the
/// live view and a Historic Event the historic one, and opening a Historic Event never disturbs the connection to a Live
/// Event.
/// </summary>
public interface IViewedEventProvider
{
    /// <summary>The Event as the Api lists it, Live or Historic, or none when there is no such Event.</summary>
    Task<IViewedEvent?> Open(Guid eventId);
}
