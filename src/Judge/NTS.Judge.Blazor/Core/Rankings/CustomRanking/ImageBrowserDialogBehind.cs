using MudBlazor;
using NTS.Judge.Blazor.Core.Rankings.Protocols;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public class ImageBrowserDialogBehind : ComponentBase
{
    string _oldImagePath = default!;

    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    IProtocolLogoPersistence HeaderLogo { get; set; } = default!;

    [Parameter]
    public string SelectedImagePath { get; set; } = default!;

    [Parameter]
    public string Src { get; set; } = default!;

    protected void Submit()
    {
        MudDialog.Close(DialogResult.Ok(SelectedImagePath));
        HeaderLogo.SetLogo(SelectedImagePath, _oldImagePath);
    }

    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    protected void HandleImageSelection(string imagePath)
    {
        _oldImagePath = SelectedImagePath;
        SelectedImagePath = imagePath;
    }
}
