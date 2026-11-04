using NC.Blazor.Components.Abstractions;
using NC.Blazor.Dialogs.Abstractions;

namespace NC.Blazor.Dialogs;

public class NDeleteDialogBehind : NDialog
{
    protected string Message => string.Format(Are_you_sure_you_want_to_delete_0_string, Item);

    [Parameter]
    public string Item { get; set; } = default!;
}
