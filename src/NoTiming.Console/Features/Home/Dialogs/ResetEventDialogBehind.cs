using Not.Blazor.Dialogs.Abstractions;

namespace NoTiming.Console.Features.Home.Dialogs;

/// <summary>A reset removes what the Event recorded and cannot be undone, so it is confirmed by typing a code.</summary>
public class ResetEventDialogBehind : NDialog
{
    const string RESET_CONFIRMATION_CODE = "1337";

    protected string ConfirmationCode { get; set; } = string.Empty;
    protected bool CanConfirm =>
        string.Equals(ConfirmationCode?.Trim(), RESET_CONFIRMATION_CODE, StringComparison.Ordinal);

    protected Task ConfirmReset()
    {
        return CanConfirm ? ConfirmDialog() : Task.CompletedTask;
    }
}
