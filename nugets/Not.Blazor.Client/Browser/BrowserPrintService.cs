using Microsoft.JSInterop;
using Not.Files;
using Not.Files.Abstractions;
using Not.Print;

namespace Not.Blazor.Client.Browser;

public class BrowserPrintService : INPrintService, IFileService
{
    readonly IJSRuntime _jsRuntime;

    public BrowserPrintService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task PrintPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var html = NBrowserPrintHtmlRenderer.Render(request);
        await using var module = await Import();
        await module.InvokeVoidAsync("printHtml", cancellationToken, html);
    }

    public Task Download(NFile file)
    {
        return Download(file, CancellationToken.None);
    }

    public Task DownloadFile(NFile file, CancellationToken cancellationToken = default)
    {
        return Download(file, cancellationToken);
    }

    async Task Download(NFile file, CancellationToken cancellationToken)
    {
        await using var module = await Import();
        await module.InvokeVoidAsync("downloadBytes", cancellationToken, file.Name, file.Content, file.ContentType);
    }

    ValueTask<IJSObjectReference> Import()
    {
        return _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/Not.Blazor.Client/print-scripts.js");
    }
}
