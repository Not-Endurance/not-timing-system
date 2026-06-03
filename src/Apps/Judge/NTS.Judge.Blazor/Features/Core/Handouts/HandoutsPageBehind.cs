using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Print;
using Not.Blazor.Helpers;
using NTS.Application.Contracts.Pdf;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Features.Print;
using NTS.Judge.Contracts.Features.Core.Handouts;
using Not.Print;

namespace NTS.Judge.Blazor.Features.Core.Handouts;

public class HandoutsPageBehind : NStatefulComponent
{
    const decimal DefaultPrintScale = 0.85m;

    IReadOnlyList<HandoutDocument> _lastPrintedHandouts = [];

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    INtsPrintRequestFactory PrintRequests { get; set; } = default!;

    [Inject]
    IHandoutsService Service { get; set; } = default!;

    protected NPrintPaperFormat PaperFormat { get; set; } = NPrintPaperFormat.A5;
    protected NPrintOrientation Orientation { get; set; } = NPrintOrientation.Landscape;
    protected IReadOnlyList<HandoutDocument> Documents => Service.Documents;
    protected bool IsEmpty => !Documents.Any();
    protected decimal PrintFontScale { get; set; } = DefaultPrintScale;
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

        var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>();
        if (await dialog.IsCanceled())
        {
            return;
        }
        await Service.Delete(_lastPrintedHandouts);
        _lastPrintedHandouts = [];
    }
}
