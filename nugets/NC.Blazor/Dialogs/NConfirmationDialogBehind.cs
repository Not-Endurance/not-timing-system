using NC.Blazor.Components.Abstractions;
using NC.Blazor.Dialogs.Abstractions;

namespace NC.Blazor.Dialogs;

public class NConfirmationDialogBehind : NDialog
{
    protected string TypedPhrase { get; set; } = string.Empty;

    protected bool CanConfirm =>
        ConfirmationPhrase is null
        || string.Equals(TypedPhrase.Trim(), ConfirmationPhrase, StringComparison.OrdinalIgnoreCase);

    [Parameter, EditorRequired]
    public string Description { get; set; } = string.Empty;

    /// <summary>Shown in red under the description: what the person must understand before confirming.</summary>
    [Parameter]
    public string? Warning { get; set; }

    /// <summary>The text above the typed phrase. Used only with <see cref="ConfirmationPhrase"/>.</summary>
    [Parameter]
    public string? ConfirmationPrompt { get; set; }

    /// <summary>
    /// When set, the confirm button stays disabled until this phrase is typed. The comparison ignores case and the
    /// spaces around the typed text.
    /// </summary>
    [Parameter]
    public string? ConfirmationPhrase { get; set; }

    /// <summary>The confirm button's text. Null uses the shared "Yes".</summary>
    [Parameter]
    public string? ConfirmText { get; set; }

    /// <summary>True for a destructive action: the confirm button is red. False makes it primary.</summary>
    [Parameter]
    public bool Danger { get; set; } = true;

    protected Task ConfirmIfAllowed()
    {
        return CanConfirm ? ConfirmDialog() : Task.CompletedTask;
    }
}
