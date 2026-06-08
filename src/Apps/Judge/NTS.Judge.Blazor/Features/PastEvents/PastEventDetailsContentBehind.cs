using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Print;
using Not.Files;
using Not.Print;
using NTS.Application.Contracts.Pdf;
using NTS.Application.Contracts.PastEvents;
using NTS.Blazor.Components.PastEvents;
using NTS.Judge.Blazor.Features.Core.Rankings.Protocols;
using NTS.Judge.Blazor.Features.Print;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using Not.Files.Abstractions;

namespace NTS.Judge.Blazor.Features.PastEvents;

public class PastEventDetailsContentBehind : NStatefulComponent
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
    INtsPrintRequestFactory PrintRequests { get; set; } = default!;

    [Inject]
    IProtocolLogoState HeaderLogo { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected ResultsDocument? Document => Service.Document;
    protected IReadOnlyList<ResultsDocument> Documents => Document == null ? [] : [Document];
    protected IReadOnlyList<Ranking> Rankings => Service.Rankings;
    protected Ranking? CurrentRanking => Service.CurrentRanking;
    protected string HeaderLogoLeft => HeaderLogo.Left;
    protected string HeaderLogoRight => HeaderLogo.Right;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;
    protected bool HasFeiExportConfigured => Service.Rankings.Any(IsFeiExportConfigured);
    protected bool CanRunResultAction => Service.Event != null && Service.Rankings.Any();
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected IReadOnlyList<NPrintPanelAction> ResultActions =>
        [
            NPrintPanelAction.PrintPdf(
                Print_string,
                CreateCurrentRanklistPrintRequest,
                Icons.Material.Outlined.Print
            ),
            NPrintPanelAction.DownloadZip(
                Download_file_string,
                CreateResultsZipRequest,
                Icons.Material.Filled.Download
            ),
        ];

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

    Task<NPrintBatchRequest> CreateResultsZipRequest(NPrintPanelContext context)
    {
        if (Service.Event == null || !Service.Rankings.Any())
        {
            throw new InvalidOperationException("Ranklist download is not available.");
        }
        return PrintRequests.CreateRanklistsZip(
            Service.Rankings,
            ranking =>
                Service.CreateDocument(ranking)
                ?? throw new InvalidOperationException($"Cannot create print document for ranking '{ranking.Id}'."),
            context,
            PdfFileNameHelper.ResultsZip(Service.Event.Id),
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
