using Not.Blazor.Dialogs.Abstractions;

namespace NTS.Witness.Blazor.Features.Setup;

public class ChangeEventHistoryDialogBehind : NDialog
{
    protected string Message =>
        string.Format(
            Connecting_to___will_remove_your_current_snapshot_history_This_cannot_be_undone_string,
            EventName
        );

    [Parameter]
    public string EventName { get; set; } = default!;
}
