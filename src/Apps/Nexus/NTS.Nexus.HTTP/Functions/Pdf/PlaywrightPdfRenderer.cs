using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Not.Injection;

namespace NTS.Nexus.HTTP.Functions.Pdf;

public sealed class PlaywrightPdfRenderer : IPdfRenderer, IAsyncDisposable
{
    readonly Lazy<Task<IPlaywright>> _playwright;
    readonly PdfSettings _settings;

    public PlaywrightPdfRenderer(IOptions<PdfSettings> settings)
    {
        _settings = settings.Value;
        _playwright = new Lazy<Task<IPlaywright>>(Playwright.CreateAsync);
    }

    public async Task<byte[]> Render(Uri url, CancellationToken cancellationToken)
    {
        var timeout = _settings.RenderTimeoutSeconds * 1000;
        var playwright = await _playwright.Value;
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true, Args = ["--no-sandbox"] }
        );
        var page = await browser.NewPageAsync();
        page.SetDefaultTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        await page.GotoAsync(
            url.ToString(),
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = timeout }
        );
        await page.WaitForSelectorAsync(
            "[data-print-ready='true'], [data-print-error]",
            new PageWaitForSelectorOptions { Timeout = timeout }
        );

        var error = await page.QuerySelectorAsync("[data-print-error]");
        if (error != null)
        {
            var message = await error.InnerTextAsync();
            throw new InvalidOperationException($"Print page failed: {message}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(
            new PagePdfOptions
            {
                PrintBackground = true,
                PreferCSSPageSize = true,
                Format = "A4",
            }
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_playwright.IsValueCreated)
        {
            var playwright = await _playwright.Value;
            playwright.Dispose();
        }
    }
}
