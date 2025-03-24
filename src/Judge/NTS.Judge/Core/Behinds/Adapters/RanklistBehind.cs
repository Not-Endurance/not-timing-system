using AngleSharp.Io;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Notify;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Blazor.Core.Rankings;

namespace NTS.Judge.Core.Behinds.Adapters;

public class RanklistBehind : ObservableBehind, IRankingBehind
{
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;

    public RanklistBehind(
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
        Participation.PHASE_COMPLETED_EVENT.Subscribe(UpdateRanklist);
        Participation.ELIMINATED_EVENT.Subscribe(UpdateRanklist);
        Participation.RESTORED_EVENT.Subscribe(UpdateRanklist);
    }

    public Ranklist? Ranklist { get; private set; }
    public RanklistDocument? Document { get; private set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var ranking = await _rankings.Read(x => true);
        if (ranking == null)
        {
            return false;
        }
        Ranklist = new Ranklist(ranking);
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            NotifyHelper.Warn("Event is not started yet");
            return false;
        }
        var officials = await _officials.ReadAll();
        var ranklistDocument = new RanklistDocument(Ranklist,enduranceEvent, officials);
        Document = ranklistDocument;
        return true;
    }

    public async Task<IEnumerable<Ranking>> GetRankings()
    {
        return await SafeHelper.Run(SafeGetRankings) ?? [];
    }

    public async Task SelectRanking(int id)
    {
        Task action() => SafeSelectRanking(id);
        await SafeHelper.Run(action);
    }

    public async Task Archive()
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            NotifyHelper.Warn("Event is not started yet");
            return;
        }
        var officials = await _officials.ReadAll();
        var rankings = await _rankings.ReadAll();
        var ranklists = rankings.Select(x => new Ranklist(x));

        var entry = new ArchiveEntry(enduranceEvent, officials, ranklists);
        await _archive.Create(entry);
    }

    async Task<IEnumerable<Ranking>> SafeGetRankings()
    {
        return await _rankings.ReadAll();
    }

    async Task SafeSelectRanking(int id)
    {
        var ranking = await _rankings.Read(id);
        GuardHelper.ThrowIfDefault(ranking);

        Ranklist = new Ranklist(ranking);
        EmitChange();
    }

    void UpdateRanklist(ParticipationPayload payload)
    {
        Ranklist?.Update(payload.Participation);
    }
}
