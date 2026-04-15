using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Features.Core.Rankings.CustomRanking;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Protocols;

public class ProtocolBehind : NStatefulComponent
{
    [Inject]
    IRanklistDocumentFactory Factory { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IProtocolLogoState HeaderLogo { get; set; } = default!;

    protected RanklistDocument? Document { get; set; } = default!;

    [Inject]
    protected IRankingContext Service { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    protected override void OnBeforeRender()
    {
        try
        {
            Document = Factory.Create(Service.Current);
        }
        catch (Exception ex)
        {
            Document = null;
            Handle(ex);
        }
    }

    public async Task OpenImageBrowser(string forImage, string source)
    {
        var parameters = new DialogParameters<ImageBrowserDialog>
        {
            { x => x.SelectedImagePath, forImage },
            { x => x.Src, source },
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
}
