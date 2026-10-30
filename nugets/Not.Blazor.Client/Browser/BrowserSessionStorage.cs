using Microsoft.JSInterop;

namespace Not.Blazor.Client.Browser;

public sealed class BrowserSessionStorage : BrowserStorage, ISessionStorage
{
    public BrowserSessionStorage(IJSRuntime browser)
        : base(browser, "sessionStorage") { }
}
