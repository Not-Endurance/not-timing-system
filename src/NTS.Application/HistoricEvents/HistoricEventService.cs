using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Application.Core;
using NTS.Contracts.HistoricEvents;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.HistoricEvents;

public class HistoricEventService : NStatefulService, IHistoricEventService, IKrudListBehind<EventInformation>
{
    static readonly IReadOnlyDictionary<int, IReadOnlyList<Starter>> EMPTY_STARTLIST =
        new Dictionary<int, IReadOnlyList<Starter>>();

    readonly IEventInformationRepository _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Ranking> _rankingRepository;
    readonly IRepository<Official> _officialRepository;
    readonly List<EventInformation> _historicEvents = [];
    IReadOnlyList<Participation> _eventParticipations = [];
    IReadOnlyList<Ranking> _rankings = [];
    IReadOnlyList<Official> _officials = [];
    Startlist? _startlist;
    Ranking? _currentRanking;

    public HistoricEventService(
        IEventInformationRepository events,
        IRepository<Participation> participations,
        IRepository<Ranking> rankingRepository,
        IRepository<Official> officialRepository
    )
    {
        _events = events;
        _participations = participations;
        _rankingRepository = rankingRepository;
        _officialRepository = officialRepository;
    }

    public IReadOnlyList<EventInformation> Events => _historicEvents.AsReadOnly();
    public EventInformation? Event { get; private set; }
    public Guid EventId =>
        Event?.Id ?? throw GuardHelper.Exception("Cannot read past-event data before selecting a past event.");
    public IReadOnlyList<Ranking> Rankings => _rankings;
    public Ranking? CurrentRanking => _currentRanking;
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> StartlistHistoryByStage =>
        _startlist?.HistoryByStage ?? EMPTY_STARTLIST;

    public ResultsDocument? Document => Event == null || CurrentRanking == null ? null : CreateDocument(CurrentRanking);

    protected override async Task<bool> InitializeState()
    {
        var historicEvents = await _events.ReadHistoric();
        _historicEvents.Clear();
        _historicEvents.AddRange(historicEvents);
        return true;
    }

    public async Task LoadEvent(Guid eventId)
    {
        await Load();

        if (Event?.Id == eventId && Rankings.Any())
        {
            return;
        }

        Event = _historicEvents.FirstOrDefault(x => x.Id == eventId);
        if (Event == null)
        {
            ClearEventState();
            EmitChanged();
            return;
        }

        var selectedEventId = EventId;
        var participations = (await _participations.ReadMany(x => x.EventId == selectedEventId)).ToList();
        _rankings = (await _rankingRepository.ReadMany(x => x.EventId == selectedEventId)).ToList();
        _officials = (await _officialRepository.ReadMany(x => x.EventId == selectedEventId)).ToList();
        _eventParticipations = participations;
        _startlist = new Startlist(participations);
        _currentRanking = _rankings.FirstOrDefault();
        EmitChanged();
    }

    public void Select(Ranking ranking)
    {
        _currentRanking = Rankings.FirstOrDefault(x => x.Id == ranking.Id) ?? ranking;
        EmitChanged();
    }

    public ResultsDocument? CreateDocument(Ranking ranking)
    {
        return Event == null
            ? null
            : new ResultsDocument(new Result(ranking, _eventParticipations, Event.RegionalRules), Event, _officials);
    }

    public async Task<IEnumerable<EventInformation>> ReadMany()
    {
        await Load();
        return Events;
    }

    public Task Delete(EventInformation entity)
    {
        throw CreateReadOnlyException();
    }

    public Task<KrudDeleteImpact> PreviewDelete(EventInformation entity)
    {
        return Task.FromResult(new KrudDeleteImpact(entity.ToString(), []));
    }

    public Task DeleteCascade(EventInformation entity)
    {
        throw CreateReadOnlyException();
    }

    void ClearEventState()
    {
        _eventParticipations = [];
        _rankings = [];
        _officials = [];
        _startlist = null;
        _currentRanking = null;
    }

    static NotSupportedException CreateReadOnlyException()
    {
        return new NotSupportedException("Past events are read-only.");
    }
}
