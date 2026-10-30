using Microsoft.JSInterop;

namespace Not.Blazor.Client.Browser;

public sealed class BrowserLocalStorage : BrowserStorage, ILocalStorage
{
    public BrowserLocalStorage(IJSRuntime browser)
        : base(browser, "localStorage") { }
}
