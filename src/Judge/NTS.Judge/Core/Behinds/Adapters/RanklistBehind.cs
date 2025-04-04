using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Notify;
using Not.Safe;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Blazor.Core.Rankings;
using NTS.Judge.Core.FeiExport;
using NTS.Judge.HTTP;

namespace NTS.Judge.Core.Behinds.Adapters;

public class RanklistBehind : ObservableBehind, IRankingBehind
{
    readonly IFeiExportBusiness _feiExportBusiness;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IArchiveRepository _archive;

    public RanklistBehind(
        IFeiExportBusiness feiExportBusiness,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IArchiveRepository archive
    )
    {
        _feiExportBusiness = feiExportBusiness;
        _rankings = rankings;
        _events = events;
        _officials = officials;
        _archive = archive;
        Participation.PHASE_COMPLETED_EVENT.Subscribe(UpdateRanklist);
        Participation.ELIMINATED_EVENT.Subscribe(UpdateRanklist);
        Participation.RESTORED_EVENT.Subscribe(UpdateRanklist);
    }

    public Ranklist? Ranklist { get; private set; }

    public int? ArchiveId { get; set; }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var ranking = await _rankings.Read(x => true);
        if (ranking == null)
        {
            return false;
        }
        Ranklist = new Ranklist(ranking);
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
        var ranklists = rankings.Select(x => new Ranklist(x)).Where(x => x.Entries.Any());

        var entry = new ArchiveEntry(enduranceEvent, officials, ranklists);
        await _archive.Create(entry);
    }

    public async Task ExportFei()
    {
        if (Ranklist == null)
        {
            return;
        }
        var xml = await _feiExportBusiness.Create(Ranklist);
        var path = $"C:/Users/User/Documents/fei-exrt-{Ranklist.Name.Replace(" ", "").Replace("*", "")}.xml";
        await File.WriteAllTextAsync(path, xml);
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
