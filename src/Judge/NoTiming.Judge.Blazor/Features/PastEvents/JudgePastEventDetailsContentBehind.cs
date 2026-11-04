using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NC.Blazor.Components.Print;
using NC.Files;
using NC.Files.Abstractions;
using NC.Print;
using NoTiming.Application.Contracts.PastEvents;
using NoTiming.Application.Contracts.Pdf;
using NoTiming.Blazor.Components.PastEvents;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Objects.Documents;
using NoTiming.Judge.Blazor.Features.Core.Rankings.Protocols;
using NoTiming.Judge.Blazor.Features.Print;

namespace NoTiming.Judge.Blazor.Features.PastEvents;

public class JudgePastEventDetailsContentBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 0.85m;

    [Inject]
    IPastEventService Service { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    IFileService FileService { get; set; } = default!;

    [Inject]
    INoTimingPrintRequestFactory PrintRequests { get; set; } = default!;

    [Inject]
    IProtocolLogoState HeaderLogo { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected ResultsDocument? Document => Service.Document;
    protected IReadOnlyList<Ranking> Rankings => Service.Rankings;
    protected Ranking? CurrentRanking => Service.CurrentRanking;
    protected string HeaderLogoLeft => HeaderLogo.Left;
    protected string HeaderLogoRight => HeaderLogo.Right;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;
    protected bool HasFeiExportConfigured => Service.Rankings.Any(IsFeiExportConfigured);
    protected bool CanRunResultAction => Service.Event != null && Service.Rankings.Any();
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected IReadOnlyList<NPrintPanelAction> ResultActions =>
        [NPrintPanelAction.PrintPdf(Print_string, CreateCurrentRanklistPrintRequest, Icons.Material.Outlined.Print)];

    [Parameter]
    public int EventId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Service.LoadEvent(EventId);
            await Observe(Service);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void SelectRankingSafe(Ranking ranking)
    {
        try
        {
            Service.Select(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenStartlist()
    {
        try
        {
            var parameters = new DialogParameters<PastEventStartlistDialog>
            {
                { x => x.HistoryByStage, Service.StartlistHistoryByStage },
            };
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Large };
            var dialog = await DialogService.ShowAsync<PastEventStartlistDialog>(Startlist_string, parameters, options);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task GenerateFeiExport()
    {
        try
        {
            if (Service.Event == null)
            {
                return;
            }

            var document = FeiExportService.Create(Service.Event, Service.Rankings);
            await FileService.Download(NFile.FromText(document.FileName, document.Content, document.ContentType));
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    Task<NPrintDocumentRequest> CreateCurrentRanklistPrintRequest(NPrintPanelContext context)
    {
        if (Service.Event == null || CurrentRanking == null || Document == null)
        {
            throw new InvalidOperationException("Ranklist print is not available.");
        }
        return PrintRequests.CreateRanklist(
            Document,
            context,
            PdfFileNameHelper.RanklistPdf(CurrentRanking.Id, CurrentRanking.Name),
            HeaderLogoLeft,
            HeaderLogoRight
        );
    }

    static bool IsFeiExportConfigured(Ranking ranking)
    {
        return !string.IsNullOrWhiteSpace(ranking.FeiEventId)
            && !string.IsNullOrWhiteSpace(ranking.FeiEventCode)
            && !string.IsNullOrWhiteSpace(ranking.FeiCompetitionId)
            && !string.IsNullOrWhiteSpace(ranking.FeiRule)
            && !string.IsNullOrWhiteSpace(ranking.FeiScheduleNumber);
    }
}
