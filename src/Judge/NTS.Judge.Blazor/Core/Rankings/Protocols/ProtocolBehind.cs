using MudBlazor;
using Not.Blazor.Components;
using Not.Exceptions;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Judge.Blazor.Core.Rankings.CustomRanking;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public class ProtocolBehind : NComponent
{
    string _shortPath = "images\\logos";
    public string src = "wwwroot\\images\\logos";
    public string headerLogoLeft = "images/logos/logo-bfks.png";
    public string headerLogoRight = "images/logos/blank.png";


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

    public async Task OpenImageBrowser(string forImage, string source)
    {
        var parameters = new DialogParameters<ImageBrowserDialog> { { x => x.SelectedImagePath, forImage }, { x => x.Src, source } };
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
            GuardHelper.ThrowIfDefault(selection);
            var filename = Path.Combine(_shortPath, Path.GetFileName(selection));
            if (forImage == headerLogoLeft)
            {                
                headerLogoLeft = filename;
            }
            else
            {
                headerLogoRight = filename;
            }
        }
    }
}
