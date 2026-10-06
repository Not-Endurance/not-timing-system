using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NTS.Contracts.Core;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// The Event of a route as the Core views see it (#630, ADR-0007): one per route Event id, never shared by two routes, and
/// never a singleton. What the Event keeps beside its Participations (its Rankings and Officials) is read once by its id;
/// where the Participations come from, and whether the person may write, is the stage's: a Live Event wraps the live
/// services and the connection, a Historic Event reads by id and has neither.
/// </summary>
public abstract class EventView : NStatefulService, IViewedEvent
{
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<Official> _officials;

    protected EventView(
        EventInformation eventInformation,
        EventStage stage,
        IRepository<Ranking> rankings,
        IRepository<Official> officials
    )
    {
        Event = eventInformation;
        Stage = stage;
        _rankings = rankings;
        _officials = officials;
    }

    public abstract IReadOnlyList<Participation> Participations { get; }
    protected abstract bool MayWrite { get; }

    public EventInformation Event { get; }
    public EventStage Stage { get; }
    public bool IsLive => Stage == EventStage.Live;

    /// <summary>Whether the person may write to the Event at all is the Api's answer, which it gives again on every write.</summary>
    public bool CanWrite => EventViewPolicy.CanWrite(Stage, MayWrite);
    public IReadOnlyList<Ranking> Rankings { get; private set; } = [];
    public IReadOnlyList<Official> Officials { get; private set; } = [];

    protected override async Task<bool> InitializeState()
    {
        var eventId = Event.Id;
        Rankings = [.. await _rankings.ReadMany(x => x.EventId == eventId)];
        Officials = [.. await _officials.ReadMany(x => x.EventId == eventId)];
        return true;
    }

    public Participation? Find(Guid id)
    {
        return Participations.FirstOrDefault(x => x.Id == id);
    }

    public ResultsDocument CreateDocument(Ranking ranking)
    {
        return new ResultsDocument(new Result(ranking, Participations, Event.RegionalRules), Event, Officials);
    }

    public bool Shows(CoreView view)
    {
        return EventViewPolicy.Shows(view, Stage);
    }

    public void EnsureCanWrite()
    {
        if (CanWrite)
        {
            return;
        }

        throw new DomainException(IsLive ? You_may_not_change_this_event_string : This_event_has_ended_string);
    }
}
