using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Print;
using Not.Blazor.Helpers;
using Not.Domain.Exceptions;
using Not.Print;
using NoTiming.Console.Features.Printing;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Handouts;

/// <summary>
/// The Handouts of the Live Event the page opened (#646): the sheets of the Participations that have a Phase complete, as they
/// stand now, printed through the browser in the paper the person chose, and, once the person says they were printed, removed.
/// While the page is open a Phase that completes makes its Handout, and the person may make the Handout of a Combination.
/// </summary>
public class HandoutsPageBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 1m;

    IReadOnlyList<ResultsDocument> _lastPrinted = [];
    IDisposable? _watching;
    bool _disposed;

    [Inject]
    IConsoleHandouts Handouts { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    INHtmlComponentRenderer Renderer { get; set; } = default!;

    protected IReadOnlyList<ResultsDocument> Documents { get; private set; } = [];
    protected string? LeftLogo => null;
    protected string? RightLogo => null;
    protected NPrintPaperFormat PaperFormat { get; set; } = NPrintPaperFormat.A5;
    protected NPrintOrientation Orientation { get; set; } = NPrintOrientation.Landscape;
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected bool CanPrint => Documents.Count > 0;
    protected bool CanChange => View.CanWrite;

    protected IReadOnlyList<NPrintPanelAction> PrintActions =>
        [NPrintPanelAction.PrintPdf(Print_string, CreatePrintRequest, Icons.Material.Outlined.Print, ConfirmPrinted)];

    [CascadingParameter]
    public IViewedEvent View { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await Handouts.Show(View);
            Watch();
            await Observe(Handouts);
            await Observe(View);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected override void OnBeforeRender()
    {
        Documents = Handouts.Documents;
    }

    public override void Dispose()
    {
        _disposed = true;
        _watching?.Dispose();
        base.Dispose();
    }

    void Watch()
    {
        if (!_disposed) // a page that left while the Handouts were read watches nothing
        {
            _watching = Handouts.Watch(View);
        }
    }

    Task<NPrintDocumentRequest> CreatePrintRequest(NPrintPanelContext context)
    {
        if (Documents.Count == 0)
        {
            throw new DomainException(Handouts_list_empty_message_string);
        }

        _lastPrinted = [.. Documents];
        return new PrintRequests(Renderer).Handouts(_lastPrinted, context, LeftLogo, RightLogo);
    }

    async Task ConfirmPrinted()
    {
        if (_lastPrinted.Count == 0)
        {
            return;
        }

        var parameters = new DialogParameters<HandoutsPrintConfirmationDialog> { { x => x.Documents, _lastPrinted } };
        var dialog = await DialogService.ShowAsync<HandoutsPrintConfirmationDialog>(string.Empty, parameters);
        if (await dialog.IsCanceled())
        {
            return;
        }

        _lastPrinted = [];
    }
}
