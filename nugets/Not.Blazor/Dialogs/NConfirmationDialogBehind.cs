using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs.Abstractions;

namespace Not.Blazor.Dialogs;

public class NConfirmationDialogBehind : NDialog
{
    [Parameter, EditorRequired]
    public string Description { get; set; } = string.Empty;
}
