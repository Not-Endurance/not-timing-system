using Microsoft.JSInterop;
using Not.Blazor.Client.Browser;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The local and the session storage are one implementation over two stores: each asks the browser for its own store, and the
/// same calls mean the same thing in both.
/// </summary>
public sealed class BrowserStorageTests
{
    [Fact]
    public async Task The_local_storage_asks_the_browser_for_the_local_store()
    {
        var browser = new RecordingBrowser();

        await new BrowserLocalStorage(browser).SetItem("nts.key", "value");

        Assert.Equal("localStorage.setItem", browser.Identifier);
    }

    [Fact]
    public async Task The_session_storage_asks_the_browser_for_the_session_store()
    {
        var browser = new RecordingBrowser();

        await new BrowserSessionStorage(browser).RemoveItem("nts.key");

        Assert.Equal("sessionStorage.removeItem", browser.Identifier);
    }

    [Fact]
    public async Task The_keys_of_the_storage_are_listed_in_full()
    {
        var browser = new FakeLocalStorage();
        browser.Put("nts.one", "1");
        browser.Put("nts.two", "2");

        var keys = await new BrowserLocalStorage(browser).GetKeys();

        Assert.Equal(["nts.one", "nts.two"], keys);
    }

    /// <summary>A browser that remembers the last call it was asked for, and answers nothing.</summary>
    sealed class RecordingBrowser : IJSRuntime
    {
        public string? Identifier { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Identifier = identifier;
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        )
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }
}
