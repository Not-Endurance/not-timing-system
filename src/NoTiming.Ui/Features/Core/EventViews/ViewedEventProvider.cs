using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Core;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Core.EventViews;

/// <summary>
/// Opens the Event of a route for the Core views (#630, ADR-0007). Whether the Event is Live is the Api's rule and its
/// clock, so it is the Api's list of Live Events that says: a Live Event gets the live view, which wraps the live services
/// and makes the Event the one the app follows when it is not; any other Event the Api has is Historic and gets the
/// historic view, which reads by id and leaves the connection alone.
/// </summary>
public sealed class ViewedEventProvider : IViewedEventProvider, IScoped
{
    readonly IEventInformationRepository _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<Official> _officials;
    readonly INtsSocketService _socket;
    readonly IParticipationStore _store;
    readonly IWitnessAccessContext _access;

    public ViewedEventProvider(
        IEventInformationRepository events,
        IRepository<Participation> participations,
        IRepository<Ranking> rankings,
        IRepository<Official> officials,
        INtsSocketService socket,
        IParticipationStore store,
        IWitnessAccessContext access
    )
    {
        _events = events;
        _participations = participations;
        _rankings = rankings;
        _officials = officials;
        _socket = socket;
        _store = store;
        _access = access;
    }

    public async Task<IViewedEvent?> Open(Guid eventId)
    {
        var view = await OpenLive(eventId) ?? await OpenHistoric(eventId);
        if (view != null)
        {
            await view.Load();
        }

        return view;
    }

    async Task<EventView?> OpenLive(Guid eventId)
    {
        var live = (await _events.ReadLive()).FirstOrDefault(x => x.Id == eventId);
        if (live == null)
        {
            return null;
        }

        if (_socket.Event?.Id != live.Id)
        {
            await _socket.Connect(live);
        }

        return new LiveEventView(live, _store, _access, _rankings, _officials);
    }

    async Task<EventView?> OpenHistoric(Guid eventId)
    {
        var historic = await _events.Read(eventId);
        return historic == null ? null : new HistoricEventView(historic, _participations, _rankings, _officials);
    }
}
