using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Print;
using Not.Domain.Exceptions;
using Not.Print;
using NoTiming.Console.Features.Printing;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Objects.Documents;

namespace NoTiming.Console.Features.Startlist;

/// <summary>
/// The Startlist of the Live Event the page opened (#646): every start that has a time, by stage, as the Participations are
/// now, printed through the browser with the logos of the Tenant. A Phase that completes gives the next one its start, so the
/// sheet is composed again whenever the Event changes.
/// </summary>
public class StartlistPageBehind : NStatefulComponent
{
    const decimal DEFAULT_PRINT_SCALE = 0.85m;

    [Inject]
    INHtmlComponentRenderer Renderer { get; set; } = default!;

    [Inject]
    IConsoleBranding Branding { get; set; } = default!;

    protected StartlistDocument? Document { get; private set; }
    protected string? LeftLogo => Branding.LeftLogo;
    protected string? RightLogo => Branding.RightLogo;
    protected decimal PrintFontScale { get; set; } = DEFAULT_PRINT_SCALE;
    protected bool CanPrint => Document is { Stages.Count: > 0 };

    protected IReadOnlyList<NPrintPanelAction> PrintActions =>
        [NPrintPanelAction.PrintPdf(Print_string, CreatePrintRequest, Icons.Material.Outlined.Print)];

    [CascadingParameter]
    public IViewedEvent View { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(View);
        await Observe(Branding);
        await Branding.Show(View); // the logos it is printed with
    }

    protected override void OnBeforeRender()
    {
        Document = StartlistDocument.Compose(View.Participations, View.Event, View.Officials);
    }

    Task<NPrintDocumentRequest> CreatePrintRequest(NPrintPanelContext context)
    {
        var document = Document is { Stages.Count: > 0 } ? Document : throw new DomainException(Empty_string);
        return new PrintRequests(Renderer).Startlist(document, context, LeftLogo, RightLogo);
    }
}
