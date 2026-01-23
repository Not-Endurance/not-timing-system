using MudBlazor;
using Not.Blazor.Components;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Core.Rankings.CustomRanking;
using NTS.Judge.Features.Core.Rankings;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public class ProtocolBehind : NStatefulComponent<IRankingContext>
{
    [Inject]
    IRanklistDocumentFactory Factory { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected IProtocolLogoState HeaderLogo { get; set; } = default!;

    protected RanklistDocument? Document { get; set; } = default!;

    protected override async Task OnBeforeRenderAsync()
    {
        try
        {
            Document = await Factory.Create(Service.Current);
        }
        catch (Exception ex)
        {
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
        var dialog = await DialogService.ShowAsync<ImageBrowserDialog>("Image Browser", parameters, options);
        await dialog.Result;
    }
}
