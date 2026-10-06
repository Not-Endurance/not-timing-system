using Not.Application.Behinds.Adapters;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Contracts.Core;

/// <summary>
/// The Event a Core view shows, whatever stage it is in (#630, ADR-0007). A view takes it from the route as a cascading
/// parameter and asks it for its data and for whether it may write. A Live Event is read through the connection and the
/// store the live views already use; a Historic Event is read once by its id, with no connection, and reports that
/// nothing can be written. The same views render both. Printing takes it as a parameter, because the print renderer
/// builds its own dependency scope. A view subscribes to the services it wraps, so whoever opened it disposes it.
/// </summary>
public interface IViewedEvent : IStatefulService, IDisposable
{
    EventInformation Event { get; }
    EventStage Stage { get; }
    bool IsLive { get; }

    /// <summary>The Event is Live and the person may write to it (the Api decides that again on every write).</summary>
    bool CanWrite { get; }

    IReadOnlyList<Participation> Participations { get; }
    IReadOnlyList<Ranking> Rankings { get; }
    IReadOnlyList<Official> Officials { get; }

    Participation? Find(Guid id);

    /// <summary>The Results of a Ranking of this Event, composed from the Participations it names (ADR-0006).</summary>
    ResultsDocument CreateDocument(Ranking ranking);

    /// <summary>Whether the Event shows the Core view at its stage; a view that is not shown is not reachable.</summary>
    bool Shows(CoreView view);

    /// <summary>
    /// The refusal of a write that a service makes before it asks the Api: a domain error when <see cref="CanWrite"/> is not
    /// true.
    /// </summary>
    void EnsureCanWrite();
}
