using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Helpers;
using NTS.Application.Contracts.Pdf;
using NTS.Application.Contracts.Socket;
using NTS.Judge.Blazor.Features.Print;
using NTS.Judge.Contracts.Features.Core.Handouts;

namespace NTS.Judge.Blazor.Features.Core.Handouts;

public class HandoutsPageBehind : NStatefulComponent
{
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INtsSocketService SocketService { get; set; } = default!;

    [Inject]
    IJudgePdfClient PdfClient { get; set; } = default!;

    [Inject]
    IJudgePdfBrowserService PdfBrowser { get; set; } = default!;

    [Inject]
    protected IHandoutsService Service { get; set; } = default!;

    protected PdfPaperFormat PaperFormat { get; set; } = PdfPaperFormat.A5;
    protected PdfOrientation Orientation { get; set; } = PdfOrientation.Landscape;
    protected bool CanPrint => SocketService.Event != null && Service.Documents.Any();

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
        await Observe(SocketService);
    }

    protected async Task PrintHandouts()
    {
        try
        {
            if (SocketService.Event == null)
            {
                return;
            }

            var handouts = Service.Documents.ToList();
            var file = await PdfClient.CreatePdf(
                new PdfDocumentRequest
                {
                    Type = PdfDocumentType.Handouts,
                    EventId = SocketService.Event.Id,
                    PaperFormat = PaperFormat,
                    Orientation = Orientation,
                    FontScale = 0.85m,
                }
            );
            await PdfBrowser.PrintPdf(file);
            var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>();
            if (await dialog.IsCanceled())
            {
                return;
            }
            await Service.Delete(handouts);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}
