using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Notify;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.Rankings.CustomRankings;
using NTS.Judge.Features.Core.Reset;

namespace NTS.Judge.Features.Core.Rankings;

public class RankingService
    : NStatefulService<ObservableList<Ranking>>,
        IRankingService,
        IRankingMenuService,
        IRanklistDocumentFactory,
        ICustomRankingService,
        ICoreDependentObservables
{
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    Ranking? _current;

    public RankingService(
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive
    )
    {
        _rankings = rankings;
        _events = events;
        _officials = officials;
        _archive = archive;
        Participation.PARTICIPATION_COMPLETED_EVENT.Subscribe(UpdateRanklist);
        Participation.PHASE_COMPLETED_EVENT.Subscribe(UpdateRanklist);
        Participation.ELIMINATED_EVENT.Subscribe(UpdateRanklist);
        Participation.RESTORED_EVENT.Subscribe(UpdateRanklist);
    }

    public Ranking Current => GuardHelper.ThrowIfDefault(
        _current, 
        $"'{nameof(RankingService)}.{nameof(Current)}' shouldn't be used before '{CreateState}' has completed. Did you forget to call 'Observe'?");

    public ObservableList<Ranking> Rankings => State;

    protected override async Task<bool> CreateState()
    {
        var rankings = await _rankings.ReadMany();
        if (!rankings.Any())
        {
            return false;
        }
        _current = rankings.First();
        Rankings.Replace(rankings);
        return true;
    }

    public async Task Create(CustomRankingModel model)
    {
        var ranking = new Ranking(
            model.Name,
            model.Ruleset,
            model.Type,
            model.Category,
            model.CompetitionFeiId,
            model.FeiRule,
            model.FeiScheduleNumber,
            new(model.Entries)
        );
        await _rankings.Create(ranking);
        Rankings.AddOrReplace(ranking);
    }

    public async Task Delete(Ranking ranking)
    {
        await _rankings.Delete(ranking);
        Rankings.Remove(ranking);
    }

    public async Task ArchiveEnduranceEvent()
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            NotifyHelper.Warn("Event is not started yet");
            return;
        }
        var officials = await _officials.ReadMany();
        var rankings = await _rankings.ReadMany();
        var ranklists = rankings.Select(x => new Ranklist(x)).Where(x => x.Entries.Any());

        var entry = new ArchiveEntry(enduranceEvent, officials, ranklists);
        await _archive.Create(entry);
    }

    public async Task<RanklistDocument> Create(Ranking ranking)
    {
        var enduranceEvent = await _events.Read(0);
        var officials = await _officials.ReadMany();
        var ranklist = new Ranklist(ranking);
        return new RanklistDocument(ranklist, enduranceEvent!, officials);
    }

    public void Select(Ranking ranking)
    {
        _current = ranking;
        EmitChanged();
    }

    void UpdateRanklist(ParticipationPayload payload)
    {
        if (Current == null)
        {
            return;
        }
        foreach (var ranking in Rankings)
        {
            ranking.Update(payload.Participation);
        }
        EmitChanged();
    }
}
