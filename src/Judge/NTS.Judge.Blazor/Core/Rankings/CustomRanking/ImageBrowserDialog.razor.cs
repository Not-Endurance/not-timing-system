using MudBlazor;
using NTS.Judge.Blazor.Core.Rankings.Protocols;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public partial class ImageBrowserDialog
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

    void Submit()
    {
        MudDialog.Close(DialogResult.Ok(SelectedImagePath));
        HeaderLogo.SetLogo(SelectedImagePath, _oldImagePath);
    }

    void Cancel()
    {
        MudDialog.Cancel();
    }

    void HandleImageSelection(string imagePath)
    {
        _oldImagePath = SelectedImagePath;
        SelectedImagePath = imagePath;
    }
}
