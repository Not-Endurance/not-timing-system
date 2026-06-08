using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Buttons;
using Not.Blazor.Components.Print;
using Not.Blazor.Dialogs;
using Not.Blazor.Helpers;
using NTS.Application.Contracts.Pdf;
using Not.Print;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;
using NTS.Judge.Blazor.Features.Core.Rankings.Protocols;
using NTS.Judge.Blazor.Features.Print;
using NTS.Judge.Blazor.Layout.Drawer.Deactivate;
using static NTS.Judge.Blazor.Routes;

namespace NTS.Judge.Blazor.Features.Core.Rankings;

public class RankingsContentBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 0.85m;

    bool _isDeactivatingEvent;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    IDashService DashService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IResultsDocumentService DocumentService { get; set; } = default!;

    [Inject]
    NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    INtsPrintRequestFactory PrintRequests { get; set; } = default!;

    [Inject]
    IProtocolLogoState HeaderLogo { get; set; } = default!;

    [Inject]
    protected IRankingMenuService RankingService { get; set; } = default!;

    protected string LeftLogo => HeaderLogo.Left;
    protected string RightLogo => HeaderLogo.Right;

    protected ResultsDocument? Document { get; private set; }
    protected IReadOnlyList<ResultsDocument> Documents => Document == null ? [] : [Document];

    protected bool HasActiveEvent => SocketService.Event != null;
    protected bool IsDeactivatingEvent => _isDeactivatingEvent;
    protected bool CanRunResultAction => HasActiveEvent && RankingService.Rankings.Any();
    protected IReadOnlyList<Ranking> Rankings => RankingService.Rankings;
    protected Ranking CurrentRanking => RankingService.Current;
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

    protected override async Task OnInitializedAsync()
    {
        await Observe(RankingService);
        await Observe(SocketService);
    }

    protected override void OnBeforeRender()
    {
        if (_isDeactivatingEvent || !RankingService.Rankings.Any())
        {
            Document = null;
            return;
        }

        Document = DocumentService.Create(RankingService.Current);
    }

    protected async Task OpenCustomRankingDialog()
    {
        try
        {
            var options = new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.Medium };
            await DialogService.ShowAsync<CustomRankingDialog>("", options);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenDeactivateEventDialog()
    {
        if (!HasActiveEvent)
        {
            return;
        }

        var deactivated = false;
        try
        {
            var dialog = await DialogService.ShowAsync<DeactivateEventDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }

            _isDeactivatingEvent = true;
            await DashService.Deactivate();
            deactivated = true;
            NavigationManager.NavigateTo(HOME);
        }
        catch (Exception ex)
        {
            if (!deactivated)
            {
                _isDeactivatingEvent = false;
            }

            Handle(ex);
        }
    }

    protected void SelectRanking(Ranking ranking)
    {
        try
        {
            RankingService.Select(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenDeleteDialog(Ranking ranking)
    {
        try
        {
            var arguments = new DialogParameters<NDeleteDialog> { { x => x.Item, ranking.Name } };
            var dialog = await DialogService.ShowAsync<NDeleteDialog>(Delete_string, arguments);
            if (await dialog.IsCanceled())
            {
                return;
            }

            await RankingService.Delete(ranking);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OpenImageBrowser(string forImage)
    {
        try
        {
            var parameters = new DialogParameters<ImageBrowserDialog>
            {
                { x => x.SelectedImagePath, forImage },
                { x => x.Src, HeaderLogo.DirPath },
            };
            DialogOptions options = new()
            {
                MaxWidth = MaxWidth.ExtraLarge,
                FullWidth = true,
                CloseOnEscapeKey = true,
            };
            var dialog = await DialogService.ShowAsync<ImageBrowserDialog>(Image_browser_string, parameters, options);
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    Task<NPrintDocumentRequest> CreateCurrentRanklistPrintRequest(NPrintPanelContext context)
    {
        if (!CanRunResultAction || SocketService.Event == null)
        {
            throw new InvalidOperationException("Ranklist print is not available.");
        }
        var document = Document ?? DocumentService.Create(RankingService.Current);
        return PrintRequests.CreateRanklist(
            document,
            context,
            PdfFileNameHelper.RanklistPdf(RankingService.Current.Id, RankingService.Current.Name),
            HeaderLogo.Left,
            HeaderLogo.Right
        );
    }

    Task<NPrintBatchRequest> CreateResultsZipRequest(NPrintPanelContext context)
    {
        if (!CanRunResultAction || SocketService.Event == null)
        {
            throw new InvalidOperationException("Ranklist download is not available.");
        }
        return PrintRequests.CreateRanklistsZip(
            RankingService.Rankings,
            DocumentService.Create,
            context,
            PdfFileNameHelper.ResultsZip(SocketService.Event.Id),
            HeaderLogo.Left,
            HeaderLogo.Right
        );
    }
}
