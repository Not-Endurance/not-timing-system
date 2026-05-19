using Microsoft.JSInterop;
using Not.Blazor.Browser;

namespace Not.Blazor.Client.Browser;

public class BrowserFileDownloadService : IFileDownloadService
{
    readonly IJSRuntime _jsRuntime;

    public BrowserFileDownloadService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task DownloadText(string fileName, string content, string contentType)
    {
        await using var module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Not.Blazor/browser-file-download.js"
        );
        await module.InvokeVoidAsync("downloadText", fileName, content, contentType);
    }
}
