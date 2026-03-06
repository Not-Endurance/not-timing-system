using Not.Blazor.Components.Abstractions;
using Not.Blazor.Dialogs.Abstractions;

namespace Not.Blazor.Dialogs;

public class NDeleteDialogBehind : NDialog
{
    protected string Message => string.Format(Are_you_sure_you_want_to_delete__string, Item);

    [Parameter]
    public string Item { get; set; } = default!;
}
