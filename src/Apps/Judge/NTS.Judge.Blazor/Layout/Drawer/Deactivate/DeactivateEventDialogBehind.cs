using Not.Blazor.Dialogs.Abstractions;

namespace NTS.Judge.Blazor.Layout.Drawer.Deactivate;

public class DeactivateEventDialogBehind : NDialog
{
    protected const string DEACTIVATE_CONFIRMATION_CODE = "1338";

    protected string ConfirmationCode { get; set; } = string.Empty;
    protected bool CanConfirm =>
        string.Equals(ConfirmationCode?.Trim(), DEACTIVATE_CONFIRMATION_CODE, StringComparison.Ordinal);

    protected Task ConfirmDeactivate()
    {
        if (!CanConfirm)
        {
            return Task.CompletedTask;
        }

        return ConfirmDialog();
    }
}
