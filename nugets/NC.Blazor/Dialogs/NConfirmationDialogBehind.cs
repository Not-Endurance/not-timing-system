using NC.Blazor.Components.Abstractions;
using NC.Blazor.Dialogs.Abstractions;

namespace NC.Blazor.Dialogs;

public class NConfirmationDialogBehind : NDialog
{
    [Parameter, EditorRequired]
    public string Description { get; set; } = string.Empty;
}
