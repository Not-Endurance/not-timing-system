using MudBlazor;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public partial class ImageBrowserDialog
{
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string SelectedImagePath { get; set; } = default!;

    [Parameter]
    public string Src { get; set; } = default!;

    void Submit()
    {
        MudDialog.Close(DialogResult.Ok(SelectedImagePath));
    }

    void Cancel()
    {
        MudDialog.Cancel();
    }

    void HandleImageSelection(string imagePath)
    {
        SelectedImagePath = imagePath;
    }
}
