using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components.Buttons;
using Not.Print;

namespace Not.Blazor.Components.Print;

public class NPrintPanelBehind : NComponent
{
    public const decimal DefaultScale = 1m;
    public const decimal ScaleStep = 0.05m;

    IReadOnlyList<NDropdownButtonDescriptor> _descriptors = [];

    [Inject]
    INPrintService PrintService { get; set; } = default!;

    protected IReadOnlyList<NDropdownButtonDescriptor> Descriptors => _descriptors;
    protected NDropdownButtonDescriptor? SingleDescriptor => Descriptors.Count == 1 ? Descriptors[0] : null;

    [Parameter]
    public IReadOnlyList<NPrintPanelAction> Actions { get; set; } = [];

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string ButtonStyle { get; set; } = "min-width:180px";

    [Parameter]
    public decimal Scale { get; set; } = DefaultScale;

    [Parameter]
    public EventCallback<decimal> ScaleChanged { get; set; }

    [Parameter]
    public string ScaleStyle { get; set; } = "width: 75px !important;height: 36px;margin-top: -3px;";

    [Parameter]
    public bool ConfigurePaperFormat { get; set; }

    [Parameter]
    public NPrintPaperFormat PaperFormat { get; set; } = NPrintPaperFormat.A4;

    [Parameter]
    public EventCallback<NPrintPaperFormat> PaperFormatChanged { get; set; }

    [Parameter]
    public bool ConfigureOrientation { get; set; }

    [Parameter]
    public NPrintOrientation Orientation { get; set; } = NPrintOrientation.Portrait;

    [Parameter]
    public EventCallback<NPrintOrientation> OrientationChanged { get; set; }

    protected override void OnParametersSet()
    {
        _descriptors = Actions
            .Select(action => new NDropdownButtonDescriptor(action.Content, () => Execute(action), action.Icon))
            .ToList();
    }

    protected async Task SetScale(decimal value)
    {
        try
        {
            Scale = value;
            await ScaleChanged.InvokeAsync(value);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task SetPaperFormat(NPrintPaperFormat value)
    {
        try
        {
            PaperFormat = value;
            await PaperFormatChanged.InvokeAsync(value);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task SetOrientation(NPrintOrientation value)
    {
        try
        {
            Orientation = value;
            await OrientationChanged.InvokeAsync(value);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task TriggerSingleAction(MouseEventArgs _)
    {
        if (SingleDescriptor == null)
        {
            return;
        }

        try
        {
            await SingleDescriptor.SafeAction();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task Execute(NPrintPanelAction action)
    {
        if (Disabled)
        {
            return;
        }

        var context = new NPrintPanelContext(Scale, PaperFormat, Orientation);
        switch (action.Kind)
        {
            case NPrintPanelActionKind.PrintPdf:
                var printRequest = await action.GetDocumentRequest(context);
                await PrintService.PrintPdf(printRequest);
                await action.AfterSuccess();
                break;
            case NPrintPanelActionKind.DownloadPdf:
                var pdfRequest = await action.GetDocumentRequest(context);
                await PrintService.DownloadPdf(pdfRequest);
                await action.AfterSuccess();
                break;
            case NPrintPanelActionKind.DownloadZip:
                var zipRequest = await action.GetBatchRequest(context);
                await PrintService.DownloadZip(zipRequest);
                await action.AfterSuccess();
                break;
            case NPrintPanelActionKind.DownloadFile:
                var file = await action.GetFile(context);
                await PrintService.DownloadFile(file);
                await action.AfterSuccess();
                break;
            default:
                throw new InvalidOperationException($"Unsupported print action '{action.Kind}'.");
        }
    }
}
