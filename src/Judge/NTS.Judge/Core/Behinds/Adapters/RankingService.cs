using Microsoft.Extensions.DependencyInjection;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Filesystem;
using Not.Notify;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Blazor.Core.Rankings;
using NTS.Judge.Blazor.Core.Rankings.Menu;
using NTS.Judge.Core.FeiExport;
using NTS.Judge.HTTP;

namespace NTS.Judge.Core.Behinds.Adapters;

public class RankingService
    : ObservableListBehind<Ranking>,
        IRankingService,
        IRankingMenuService,
        IRanklistDocumentService
{
    readonly IFileContext _configuration;
    readonly IFeiExportBusiness _feiExportBusiness;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IArchiveRepository _archive;

    public RankingService(
        [FromKeyedServices("NDataKey")] IFileContext configuration,
        IFeiExportBusiness feiExportBusiness,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IArchiveRepository archive
    )
    {
        _configuration = configuration;
        _feiExportBusiness = feiExportBusiness;
        _rankings = rankings;
        _events = events;
        _officials = officials;
        _archive = archive;
        Participation.PHASE_COMPLETED_EVENT.Subscribe(UpdateRanklist);
        Participation.ELIMINATED_EVENT.Subscribe(UpdateRanklist);
        Participation.RESTORED_EVENT.Subscribe(UpdateRanklist);
    }

    public Ranking? SelectedRanking { get; set; }
    public Ranklist? Ranklist { get; set; }
    public RanklistDocument? Document { get; private set; }
    public ObservableList<Ranking> Rankings => ObservableList;

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var rankings = await _rankings.ReadAll();
        if (!rankings.Any())
        {
            return false;
        }
        Rankings.AddRange(rankings);
        await Select(rankings.First());
        return true;
    }

    public async Task Select(Ranking ranking)
    {
        var enduranceEvent = await _events.Read(0);
        var officials = await _officials.ReadAll();
        GuardHelper.ThrowIfDefault(enduranceEvent);
        SelectedRanking = ranking;
        Ranklist = new Ranklist(SelectedRanking);
        Document = new RanklistDocument(Ranklist, enduranceEvent, officials);
        EmitChange();
    }

    public async Task GenerateFeiExport()
    {
        if (Ranklist == null)
        {
            return;
        }
        var xml = await _feiExportBusiness.Create(Ranklist);
        var path = $"{_configuration.Path}/fei-export-{Ranklist.Name.Replace(" ", "").Replace("*", "")}.xml";
        await FileHelper.WriteAsync(path, xml);
    }

    public async Task ArchiveEnduranceEvent()
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

    void UpdateRanklist(ParticipationPayload payload)
    {
        Ranklist?.Update(payload.Participation);
    }
}
