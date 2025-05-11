using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Not.Blazor.Components;

public abstract class NDialog : NComponent
{
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = default!;

    public void Confirm()
    {
        var dialogResult = DialogResult.Ok(true);
        MudDialog.Close(dialogResult);
    }

    public void Cancel()
    {
        MudDialog.Cancel();
    }
}
