using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Not.Blazor.Components;

public abstract class NDialog : NComponent
{
    [CascadingParameter]
    protected MudDialogInstance CurrentDialog { get; set; } = default!;

    public void Confirm()
    {
        var dialogResult = DialogResult.Ok(true);
        CurrentDialog.Close(dialogResult);
    }

    public void Cancel()
    {
        CurrentDialog.Cancel();
    }
}
