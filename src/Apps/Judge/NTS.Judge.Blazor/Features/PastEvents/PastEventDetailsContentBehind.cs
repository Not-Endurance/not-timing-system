using MudBlazor;
using Not.Blazor.Browser;
using Not.Blazor.Components.Abstractions;
using NTS.Application.Contracts.Pdf;
using NTS.Application.Contracts.PastEvents;
using NTS.Blazor.Components.PastEvents;
using NTS.Judge.Blazor.Features.Print;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Blazor.Features.PastEvents;

public class PastEventDetailsContentBehind : NStatefulComponent
{
    [Inject]
    protected IPastEventService Service { get; set; } = default!;

    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IFeiExportService FeiExportService { get; set; } = default!;

    [Inject]
    protected IFileDownloadService FileDownloadService { get; set; } = default!;

    [Inject]
    protected IJudgePdfClient PdfClient { get; set; } = default!;

    [Inject]
    protected IJudgePdfBrowserService PdfBrowser { get; set; } = default!;

    protected bool IsEmpty => Service.Event == null || Service.Document == null;
    protected bool HasStartlist => Service.StartlistHistoryByStage.Count != 0;
    protected bool HasFeiExportConfigured => Service.Rankings.Any(IsFeiExportConfigured);
    protected bool CanRunResultAction => Service.Event != null && Service.CurrentRanking != null;
    protected PdfResultAction ResultAction { get; set; } = PdfResultAction.Print;
    protected string ResultActionText =>
        ResultAction == PdfResultAction.Print ? Print_string : Download_results_string;
    protected string ResultActionIcon =>
        ResultAction == PdfResultAction.Print ? Icons.Material.Outlined.Print : Icons.Material.Filled.Download;

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

    protected void SelectRanking(Ranking ranking)
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
            await FileDownloadService.DownloadText(document.FileName, document.Content, document.ContentType);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task RunResultAction()
    {
        try
        {
            if (Service.Event == null || Service.CurrentRanking == null)
            {
                return;
            }

            if (ResultAction == PdfResultAction.Print)
            {
                var file = await PdfClient.CreatePdf(
                    new PdfDocumentRequest
                    {
                        Type = PdfDocumentType.Ranklist,
                        EventId = Service.Event.Id,
                        RankingId = Service.CurrentRanking.Id,
                        FontScale = 0.8m,
                    }
                );
                await PdfBrowser.PrintPdf(file);
                return;
            }

            var zip = await PdfClient.CreateResultsZip(new PdfResultsZipRequest { EventId = Service.Event.Id });
            await PdfBrowser.Download(zip);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
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
