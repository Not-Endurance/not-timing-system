using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Not.BLazor.Client.Browser;

internal class BrowserLocalStorage : IBrowserLocalStorage
{
    const string LOCAL_STORAGE_GET_ITEM_FUNCTION = "localStorage.getItem";
    const string LOCAL_STORAGE_SET_ITEM_FUNCTION = "localStorage.setItem";
    const string LOCAL_STORAGE_REMOVE_ITEM_FUNCTION = "localStorage.removeItem";

    readonly IJSRuntime _jsRuntime;
    readonly ILogger<BrowserLocalStorage> _logger;

    public BrowserLocalStorage(IJSRuntime jsRuntime, ILogger<BrowserLocalStorage> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task Clear(string key)
    {
        await Write(key, null);
    }

    public async Task<string?> Read(string key)
    {
        return await _jsRuntime.InvokeAsync<string?>(LOCAL_STORAGE_GET_ITEM_FUNCTION, key);
    }

    public async Task Write(string key, string? value)
    {
        try
        {
            if (_jsRuntime is IJSInProcessRuntime jsInProcessRuntime)
            {
                if (value is null)
                {
                    jsInProcessRuntime.InvokeVoid(LOCAL_STORAGE_REMOVE_ITEM_FUNCTION, key);
                    return;
                }

                jsInProcessRuntime.InvokeVoid(LOCAL_STORAGE_SET_ITEM_FUNCTION, key, value);
                return;
            }

            if (value is null)
            {
                await _jsRuntime.InvokeVoidAsync(LOCAL_STORAGE_REMOVE_ITEM_FUNCTION, key);
                return;
            }

            await _jsRuntime.InvokeVoidAsync(LOCAL_STORAGE_SET_ITEM_FUNCTION, key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client auth: Unable to update browser storage key {Key}.", key);
        }
    }
}

internal interface IBrowserLocalStorage
{
    Task<string?> Read(string key);
    Task Write(string key, string? value);
    Task Clear(string key);
}