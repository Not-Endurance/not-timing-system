using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Not.Application.Print;
using Not.Files;
using Not.Files.Abstractions;
using Not.Print;

namespace Not.Blazor.Client.Browser;

public class BrowserPrintService : INPrintService, IFileService
{
    readonly INPrintApiService _api;
    readonly IJSRuntime _jsRuntime;
    readonly NPrintClientSettings _settings;

    public BrowserPrintService(IJSRuntime jsRuntime, INPrintApiService api, IOptions<NPrintClientSettings> settings)
    {
        _jsRuntime = jsRuntime;
        _api = api;
        _settings = settings.Value;
    }

    public Task<NFile> CreatePdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default)
    {
        return _api.CreatePdf(request, cancellationToken);
    }

    public Task<NFile> CreateZip(NPrintBatchRequest request, CancellationToken cancellationToken = default)
    {
        return _api.CreateZip(request, cancellationToken);
    }

    public async Task PrintPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (_settings.BypassBackendPrinting)
        {
            var html = NBrowserPrintHtmlRenderer.Render(request);
            await using var directModule = await Import();
            await directModule.InvokeVoidAsync("printHtml", cancellationToken, html);
            return;
        }

        var file = await CreatePdf(request, cancellationToken);
        await using var module = await Import();
        await module.InvokeVoidAsync("printPdfBytes", cancellationToken, file.Content);
    }

    public async Task DownloadPdf(NPrintDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var file = await CreatePdf(request, cancellationToken);
        await Download(file, cancellationToken);
    }

    public async Task DownloadZip(NPrintBatchRequest request, CancellationToken cancellationToken = default)
    {
        var file = await CreateZip(request, cancellationToken);
        await Download(file, cancellationToken);
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
