using Microsoft.Extensions.DependencyInjection;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Filesystem;
using Not.Notify;
using Not.Random;
using Not.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.FeiExport;
using NTS.Judge.Features.Core.Reset;

namespace NTS.Judge.Features.Core.Rankings;

public class RankingService
    : ObservableListBehind<Ranking>,
        IRankingService,
        IRankingMenuService,
        IRanklistDocumentService,
        ICustomRankingService,
        ICoreDependentObservables
{
    readonly IFileContext _configuration;
    readonly IFeiExportBusiness _feiExportBusiness;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;

    public RankingService(
        [FromKeyedServices("NDataKey")] IFileContext configuration,
        IFeiExportBusiness feiExportBusiness,
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive
    )
    {
        _configuration = configuration;
        _feiExportBusiness = feiExportBusiness;
        _rankings = rankings;
        _events = events;
        _officials = officials;
        _archive = archive;
        Participation.PARTICIPATION_COMPLETED_EVENT.Subscribe(UpdateRanklist);
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
        Rankings.Clear();
        Rankings.AddRange(rankings);
        await Select(rankings.First());
        return true;
    }

    public async Task Create(CustomRankingModel model)
    {
        var ranking = new Ranking(
            RandomHelper.GenerateUniqueInteger(),
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

    public async Task Delete(Ranking ranking)
    {
        await _rankings.Delete(ranking);
        Rankings.Remove(ranking);
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

    // ReSharper disable once AsyncVoidMethod
    async void UpdateRanklist(ParticipationPayload payload)
    {
        if (SelectedRanking == null)
        {
            return;
        }
        foreach (var ranking in Rankings)
        {
            ranking.Update(payload.Participation);
        }
        await Select(SelectedRanking);
    }
}
