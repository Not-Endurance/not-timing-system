using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public abstract class NDialog : NComponent
{
    [CascadingParameter]
    protected MudDialogInstance CurrentDialog { get; set; } = default!;

    protected Task CloseDialog()
    {
        var dialogResult = DialogResult.Ok(true);
        CurrentDialog.Close(dialogResult);
        return Task.CompletedTask;
    }

    public Task CancelDialog()
    {
        CurrentDialog.Cancel();
        return Task.CompletedTask;
    }
}

public abstract class NDialog<T> : NComponent
{
    [CascadingParameter]
    protected MudDialogInstance CurrentDialog { get; set; } = default!;

    protected Task CloseDialog(T value)
    {
        var dialogResult = DialogResult.Ok(value);
        CurrentDialog.Close(dialogResult);
        return Task.CompletedTask;
    }

    public Task CancelDialog()
    {
        CurrentDialog.Cancel();
        return Task.CompletedTask;
    }
}
