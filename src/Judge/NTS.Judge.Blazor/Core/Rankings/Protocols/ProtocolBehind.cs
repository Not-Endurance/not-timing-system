using MudBlazor;
using Not.Blazor.Components;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Core.Rankings.CustomRanking;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public class ProtocolBehind : NComponent
{

    public string headerLogoLeft = "images/logos/logo-nak.jpg";
    public string headerLogoRight = "images/logos/logo-bfks.png";

    [Inject]
    IRanklistDocumentService Service { get; set; } = default!;
    [Inject]
    IDialogService DialogService { get; set; } = default!;

    public DocumentHeader? Header => Service.Document?.Header;
    public Ranklist? Ranklist => Service.Document?.Ranklist;

    protected override async Task OnInitializedAsync()
    {
        await Observe(Service);
    }

    public async Task OpenImageBrowser(string forImage)
    {
        var parameters = new DialogParameters<ImageBrowserDialog> { { x => x.SelectedImagePath, forImage } };
        DialogOptions options = new()
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        var dialog = await DialogService.ShowAsync<ImageBrowserDialog>("Image Browser", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            var selection = result.Data as string;
            if (forImage == headerLogoLeft)
            {                
                headerLogoLeft = selection!;
            }
            else
            {
                headerLogoRight = selection!;
            }
        }
    }
}
