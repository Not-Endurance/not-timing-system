using MudBlazor;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Dialogs.Abstractions;

public abstract class NDialog : NStatefulComponent
{
    [CascadingParameter]
    protected MudDialogInstance CurrentDialog { get; set; } = default!;

    protected Task ConfirmDialog()
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

public abstract class NDialog<T> : NStatefulComponent
{
    [CascadingParameter]
    protected MudDialogInstance CurrentDialog { get; set; } = default!;

    protected Task ConfirmDialog(T value)
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
