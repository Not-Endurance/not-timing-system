using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Not.Injection;

namespace Not.Blazor.Client.Authentication.Services;

internal class LocalStorageMarkerService : ILocalStorageMarkerService, ITransient
{
    const string LOCAL_SIGNOUT_MARKER_KEY = "not.auth.local-signout";
    const string LOCAL_SIGNOUT_MARKER_VALUE = "1";
    const string SET_ITEM_FUNCTION = "localStorage.setItem";
    const string GET_ITEM_FUNCTION = "localStorage.getItem";
    const string REMOVE_ITEM_FUNCTION = "localStorage.removeItem";

    readonly IJSRuntime _jsRuntime;
    readonly ILogger<LocalStorageMarkerService> _logger;

    public LocalStorageMarkerService(IJSRuntime jsRuntime, ILogger<LocalStorageMarkerService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public void SetSignedOut()
    {
        WriteSignoutMarker(LOCAL_SIGNOUT_MARKER_VALUE);
    }

    public void ClearSignedOut()
    {
        WriteSignoutMarker(value: null);
    }

    public async Task<bool> IsSignedOut()
    {
        try
        {
            var marker = await _jsRuntime.InvokeAsync<string?>(GET_ITEM_FUNCTION, LOCAL_SIGNOUT_MARKER_KEY);

            return string.Equals(marker, LOCAL_SIGNOUT_MARKER_VALUE, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client auth factory: Unable to read local signout marker.");
            return false;
        }
    }

    void WriteSignoutMarker(string? value)
    {
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsInProcessRuntime)
            {
                if (value is null)
                {
                    jsInProcessRuntime.InvokeVoid(REMOVE_ITEM_FUNCTION, LOCAL_SIGNOUT_MARKER_KEY);
                    return;
                }
                jsInProcessRuntime.InvokeVoid(SET_ITEM_FUNCTION, LOCAL_SIGNOUT_MARKER_KEY, value);
                return;
            }

            if (value is null)
            {
                _jsRuntime.InvokeVoidAsync(REMOVE_ITEM_FUNCTION, LOCAL_SIGNOUT_MARKER_KEY);
                return;
            }
            _jsRuntime.InvokeVoidAsync(SET_ITEM_FUNCTION, LOCAL_SIGNOUT_MARKER_KEY, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client auth: Unable to update local signout marker.");
        }
    }
}

internal interface ILocalStorageMarkerService
{
    void SetSignedOut();
    void ClearSignedOut();
    Task<bool> IsSignedOut();
}
