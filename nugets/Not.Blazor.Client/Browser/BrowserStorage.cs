using Microsoft.JSInterop;

namespace Not.Blazor.Client.Browser;

/// <summary>
/// A store of the browser, reached through its JavaScript API. The local and the session storage have the same API, so one
/// implementation serves both: the subclass names the store.
/// </summary>
public abstract class BrowserStorage : IBrowserStorage
{
    readonly IJSRuntime _browser;
    readonly string _store;

    protected BrowserStorage(IJSRuntime browser, string store)
    {
        _browser = browser;
        _store = store;
    }

    public Task<string?> GetItem(string key)
    {
        return _browser.InvokeAsync<string?>($"{_store}.getItem", key).AsTask();
    }

    public Task SetItem(string key, string value)
    {
        return _browser.InvokeVoidAsync($"{_store}.setItem", key, value).AsTask();
    }

    public Task RemoveItem(string key)
    {
        return _browser.InvokeVoidAsync($"{_store}.removeItem", key).AsTask();
    }

    /// <summary>The browser gives the key at each position, and none past the last one.</summary>
    public async Task<IReadOnlyList<string>> GetKeys()
    {
        var keys = new List<string>();
        for (var index = 0; ; index++)
        {
            var key = await _browser.InvokeAsync<string?>($"{_store}.key", index).AsTask();
            if (key == null)
            {
                return keys;
            }

            keys.Add(key);
        }
    }
}
