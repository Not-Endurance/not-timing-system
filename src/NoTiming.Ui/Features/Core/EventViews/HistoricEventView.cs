using Not.Application.CRUD.Ports;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// A Historic Event as a record (#630, ADR-0007): its Participations, Rankings and Officials are read once by the Event's
/// id through repositories that belong to no connection, and nothing here is a handler or shared with the live services,
/// so opening it never disturbs a Live Event that is being followed. Nothing about it can be written.
/// </summary>
public sealed class HistoricEventView : EventView
{
    readonly IRepository<Participation> _participations;
    IReadOnlyList<Participation> _loaded = [];

    public HistoricEventView(
        EventInformation eventInformation,
        IRepository<Participation> participations,
        IRepository<Ranking> rankings,
        IRepository<Official> officials
    )
        : base(eventInformation, EventStage.Historic, rankings, officials)
    {
        _participations = participations;
    }

    protected override bool MayWrite => false;

    public override IReadOnlyList<Participation> Participations => _loaded;

    protected override async Task<bool> InitializeState()
    {
        var eventId = Event.Id;
        _loaded = [.. await _participations.ReadMany(x => x.EventId == eventId)];
        return await base.InitializeState();
    }
}
