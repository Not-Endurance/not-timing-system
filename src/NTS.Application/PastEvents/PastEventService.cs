using Microsoft.Extensions.DependencyInjection;
using Not.Application.Behinds.Adapters;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using NTS.Application.Contracts.PastEvents;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.PastEvents;

public class PastEventService : NStatefulService, IPastEventService, IKrudListBehind<EnduranceEvent>, IScoped
{
    static readonly IReadOnlyDictionary<int, IReadOnlyList<Starter>> EMPTY_STARTLIST =
        new Dictionary<int, IReadOnlyList<Starter>>();

    readonly IEnduranceEventRepository _events;
    readonly IServiceProvider _serviceProvider;
    readonly List<EnduranceEvent> _pastEvents = [];
    IReadOnlyList<Ranking> _rankings = [];
    IReadOnlyList<Official> _officials = [];
    Startlist? _startlist;
    Ranking? _currentRanking;

    public PastEventService(IEnduranceEventRepository events, IServiceProvider serviceProvider)
    {
        _events = events;
        _serviceProvider = serviceProvider;
    }

    public IReadOnlyList<EnduranceEvent> Events => _pastEvents.AsReadOnly();
    public EnduranceEvent? Event { get; private set; }
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

        var participations = await _serviceProvider
            .GetRequiredService<IPastParticipationRepository>()
            .ReadForEvent(EventId);
        _rankings = (
            await _serviceProvider.GetRequiredService<IPastRankingRepository>().ReadForEvent(EventId)
        ).ToList();
        _officials = (
            await _serviceProvider.GetRequiredService<IPastOfficialRepository>().ReadForEvent(EventId)
        ).ToList();
        _startlist = new Startlist(participations);
        _currentRanking = _rankings.FirstOrDefault();
        EmitChanged();
    }

    public void Select(Ranking ranking)
    {
        _currentRanking = Rankings.FirstOrDefault(x => x.Id == ranking.Id) ?? ranking;
        EmitChanged();
    }

    public async Task<IEnumerable<EnduranceEvent>> ReadMany()
    {
        await Load();
        return Events;
    }

    public Task Delete(EnduranceEvent entity)
    {
        throw CreateReadOnlyException();
    }

    public Task<KrudDeleteImpact> PreviewDelete(EnduranceEvent entity)
    {
        return Task.FromResult(new KrudDeleteImpact(entity.ToString(), []));
    }

    public Task DeleteCascade(EnduranceEvent entity)
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
