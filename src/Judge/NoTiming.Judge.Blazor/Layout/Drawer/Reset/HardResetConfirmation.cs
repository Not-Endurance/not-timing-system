using MudBlazor;
using NC.Blazor.Dialogs;

namespace NoTiming.Judge.Blazor.Layout.Drawer.Reset;

/// <summary>The hard reset's confirmation. Both places that start a reset open it with these parameters.</summary>
public static class HardResetConfirmation
{
    public static DialogParameters<NConfirmationDialog> Parameters()
    {
        return new DialogParameters<NConfirmationDialog>
        {
            {
                x => x.Description,
                $"{Are_you_sure_you_want_to_reset_This_will_permanently_delete_Snapshots_Rankings_Protocols_and_Handouts_string} {Setup_configuration_will_not_be_deleted_string}"
            },
            { x => x.Warning, This_action_is_final_There_is_no_recovery_string },
            { x => x.ConfirmationPrompt, string.Format(Type_phrase_to_enable_string, I_understand_string) },
            { x => x.ConfirmationPhrase, I_understand_string },
            { x => x.ConfirmText, Confirm_string },
        };
    }
}
