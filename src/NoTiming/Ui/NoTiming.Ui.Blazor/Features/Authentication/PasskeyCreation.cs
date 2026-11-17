using System.Net;
using Microsoft.JSInterop;
using MudBlazor;
using NC.Blazor.Dialogs;
using NC.Blazor.Helpers;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

/// <summary>
/// Creates a passkey for the signed-in person. The browser must support passkeys, the person must accept the warning
/// about shared devices, and then the browser's ceremony runs. The Passkeys page and the prompt after sign-in both use it.
/// </summary>
public static class PasskeyCreation
{
    public static async Task<PasskeyCreationOutcome> CreateAsync(
        IUiPasskeyService passkeys,
        IJSRuntime javaScript,
        IDialogService dialogs,
        string description
    )
    {
        if (!await PasskeyCeremony.IsSupportedAsync(javaScript))
        {
            return PasskeyCreationOutcome.Unsupported;
        }

        if (!await AcceptWarningAsync(dialogs))
        {
            return PasskeyCreationOutcome.Declined;
        }

        try
        {
            var options = await passkeys.EnrolOptionsAsync();
            var credential = await PasskeyCeremony.CreateAsync(javaScript, options);
            await passkeys.EnrolAsync(credential, description);
            return PasskeyCreationOutcome.Created;
        }
        catch (JSException)
        {
            return PasskeyCreationOutcome.Cancelled;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return PasskeyCreationOutcome.NotSignedIn;
        }
    }

    static async Task<bool> AcceptWarningAsync(IDialogService dialogs)
    {
        var parameters = new DialogParameters<NConfirmationDialog>
        {
            { x => x.Description, Passkey_device_question_string },
            { x => x.Warning, Passkey_shared_device_warning_string },
            { x => x.ConfirmationPrompt, string.Format(Type_phrase_to_enable_string, I_understand_string) },
            { x => x.ConfirmationPhrase, I_understand_string },
            { x => x.ConfirmText, Add_passkey_to_device_string },
            { x => x.Danger, false },
        };
        var dialog = await dialogs.ShowAsync<NConfirmationDialog>(Add_passkey_to_device_string, parameters);
        return !await dialog.IsCanceled();
    }
}
