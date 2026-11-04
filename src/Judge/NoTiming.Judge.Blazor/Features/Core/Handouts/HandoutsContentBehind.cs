using MudBlazor;
using NC.Blazor.Components.Abstractions;
using NC.Blazor.Components.Print;
using NC.Blazor.Helpers;
using NC.Print;
using NoTiming.Application.Contracts.Pdf;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Domain.Core.Objects.Documents;
using NoTiming.Judge.Blazor.Features.Core.Rankings.Protocols;
using NoTiming.Judge.Blazor.Features.Print;

namespace NoTiming.Judge.Blazor.Features.Core.Handouts;

public class HandoutsContentBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 1m;

    IReadOnlyList<ResultsDocument> _lastPrintedHandouts = [];

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INoTimingSocketService SocketService { get; set; } = default!;

    [Inject]
    INoTimingPrintRequestFactory PrintRequests { get; set; } = default!;

    [Inject]
    IHandoutsService Service { get; set; } = default!;

    [Inject]
    IProtocolLogoState HeaderLogo { get; set; } = default!;

    protected string LeftLogo => HeaderLogo.Left;
    protected string RightLogo => HeaderLogo.Right;
    protected NPrintPaperFormat PaperFormat { get; set; } = NPrintPaperFormat.A5;
    protected NPrintOrientation Orientation { get; set; } = NPrintOrientation.Landscape;
    protected IReadOnlyList<ResultsDocument> Documents => Service.Documents;
    protected bool IsEmpty => !Documents.Any();
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected bool CanPrint => SocketService.Event != null && Documents.Any();
    protected IReadOnlyList<NPrintPanelAction> PrintActions =>
        [
            NPrintPanelAction.PrintPdf(
                Print_string,
                CreateHandoutsPrintRequest,
                Icons.Material.Outlined.Print,
                ConfirmPrintedHandouts
            ),
        ];

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
        await Observe(SocketService);
    }

    Task<NPrintDocumentRequest> CreateHandoutsPrintRequest(NPrintPanelContext context)
    {
        if (SocketService.Event == null || Documents.Count == 0)
        {
            throw new InvalidOperationException("Handout print is not available.");
        }

        _lastPrintedHandouts = Documents.ToList();
        return PrintRequests.CreateHandouts(
            _lastPrintedHandouts,
            context,
            PdfFileNameHelper.HandoutsPdf(SocketService.Event.Id)
        );
    }

    async Task ConfirmPrintedHandouts()
    {
        if (_lastPrintedHandouts.Count == 0)
        {
            return;
        }

        var parameters = new DialogParameters<HandoutsPrintConfirmationDialog>
        {
            { x => x.Documents, _lastPrintedHandouts },
        };
        var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>(string.Empty, parameters);
        if (await dialog.IsCanceled())
        {
            return;
        }
        _lastPrintedHandouts = [];
    }
}
