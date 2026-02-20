using Not.Blazor.Components.Abstractions;
using Not.Blazor.Components;

namespace Not.Blazor.Dialogs;

public class NConfirmDeleteDialogBehind : NDialog
{
    protected string Message => string.Format(Are_you_sure_you_want_to_delete__string, Item);

    [Parameter]
    public string Item { get; set; } = default!;
}
