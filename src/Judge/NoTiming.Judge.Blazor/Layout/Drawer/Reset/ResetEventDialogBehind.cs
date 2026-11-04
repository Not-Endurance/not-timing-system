using Not.Blazor.Dialogs.Abstractions;

namespace NTS.Judge.Blazor.Layout.Drawer.Reset;

public class ResetEventDialogBehind : NDialog
{
    protected const string RESET_CONFIRMATION_CODE = "1337";

    protected string ConfirmationCode { get; set; } = string.Empty;
    protected bool CanConfirm =>
        string.Equals(ConfirmationCode?.Trim(), RESET_CONFIRMATION_CODE, StringComparison.Ordinal);

    protected Task ConfirmReset()
    {
        if (!CanConfirm)
        {
            return Task.CompletedTask;
        }

        return ConfirmDialog();
    }
}
