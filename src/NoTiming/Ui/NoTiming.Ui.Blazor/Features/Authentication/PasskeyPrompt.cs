using Microsoft.JSInterop;
using MudBlazor;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

/// <summary>
/// Offers a passkey to a person who has just signed in or registered with an email code and has none yet. The offer is
/// optional: the person can press "Not now", and sign-in has already succeeded.
/// </summary>
public static class PasskeyPrompt
{
    public static async Task OfferIfNoneAsync(IUiPasskeyService passkeys, IJSRuntime javaScript, IDialogService dialogs)
    {
        if (!await PasskeyCeremony.IsSupportedAsync(javaScript))
        {
            return;
        }

        if ((await passkeys.ListAsync()).Count > 0)
        {
            return;
        }

        var dialog = await dialogs.ShowAsync<PasskeyPromptDialog>(Sign_in_faster_next_time_string);
        await dialog.Result;
    }
}
