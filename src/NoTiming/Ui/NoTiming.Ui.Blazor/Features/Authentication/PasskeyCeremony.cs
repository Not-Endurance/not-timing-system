using Microsoft.JSInterop;

namespace NoTiming.Ui.Blazor.Features.Authentication;

/// <summary>
/// The browser's passkey ceremonies, through passkeys.js. The options and the answers are the WebAuthn JSON form. A
/// person who cancels the browser's prompt makes the call throw a JSException, which the pages report as a retry.
/// </summary>
public static class PasskeyCeremony
{
    public static ValueTask<bool> IsSupportedAsync(IJSRuntime js)
    {
        return js.InvokeAsync<bool>("noTimingPasskeys.isSupported");
    }

    public static ValueTask<string> CreateAsync(IJSRuntime js, string optionsJson)
    {
        return js.InvokeAsync<string>("noTimingPasskeys.create", optionsJson);
    }

    public static ValueTask<string> GetAsync(IJSRuntime js, string optionsJson)
    {
        return js.InvokeAsync<string>("noTimingPasskeys.get", optionsJson);
    }
}
