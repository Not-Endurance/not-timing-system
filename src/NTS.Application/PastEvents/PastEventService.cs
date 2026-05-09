using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Application.Contracts.PastEvents;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.PastEvents;

public class PastEventService : NStatefulService, IPastEventService, IKrudListBehind<EventInformation>
{
    static readonly IReadOnlyDictionary<int, IReadOnlyList<Starter>> EMPTY_STARTLIST =
        new Dictionary<int, IReadOnlyList<Starter>>();

    readonly IEventInformationRepository _events;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Ranking> _rankingRepository;
    readonly IRepository<Official> _officialRepository;
    readonly List<EventInformation> _pastEvents = [];
    IReadOnlyList<Ranking> _rankings = [];
    IReadOnlyList<Official> _officials = [];
    Startlist? _startlist;
    Ranking? _currentRanking;

    public PastEventService(
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

    public IReadOnlyList<EventInformation> Events => _pastEvents.AsReadOnly();
    public EventInformation? Event { get; private set; }
    public int EventId =>
        Event?.Id ?? throw GuardHelper.Exception("Cannot read past-event data before selecting a past event.");
    public IReadOnlyList<Ranking> Rankings => _rankings;
    public Ranking? CurrentRanking => _currentRanking;
    public IReadOnlyDictionary<int, IReadOnlyList<Starter>> StartlistHistoryByStage =>
        _startlist?.HistoryByStage ?? EMPTY_STARTLIST;

    public ProtocolDocument? Document =>
        Event == null || CurrentRanking == null
            ? null
            : new ProtocolDocument(new Ranklist(CurrentRanking), Event, _officials);

    protected override async Task<bool> InitializeState()
    {
        var pastEvents = await _events.ReadPast();
        _pastEvents.Clear();
        _pastEvents.AddRange(pastEvents);
        return true;
    }

    public async Task LoadEvent(int eventId)
    {
        await Load();

        if (Event?.Id == eventId && Rankings.Any())
        {
            return;
        }

        Event = _pastEvents.FirstOrDefault(x => x.Id == eventId);
        if (Event == null)
        {
            ClearEventState();
            EmitChanged();
            return;
        }

        var selectedEventId = EventId;
        var participations = await _participations.ReadMany(x => x.EventId == selectedEventId);
        _rankings = (await _rankingRepository.ReadMany(x => x.EventId == selectedEventId)).ToList();
        _officials = (await _officialRepository.ReadMany(x => x.EventId == selectedEventId)).ToList();
        _startlist = new Startlist(participations);
        _currentRanking = _rankings.FirstOrDefault();
        EmitChanged();
    }

    public void Select(Ranking ranking)
    {
        _currentRanking = Rankings.FirstOrDefault(x => x.Id == ranking.Id) ?? ranking;
        EmitChanged();
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
