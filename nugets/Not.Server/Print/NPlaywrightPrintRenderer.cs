using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Not.Server.Print;

public sealed class NPlaywrightPrintRenderer : INPrintRenderer, IAsyncDisposable
{
    readonly Lazy<Task<IPlaywright>> _playwright;
    readonly NPrintSettings _settings;

    public NPlaywrightPrintRenderer(IOptions<NPrintSettings> settings)
    {
        _settings = settings.Value;
        _playwright = new Lazy<Task<IPlaywright>>(Playwright.CreateAsync);
    }

    public async Task<byte[]> Render(string html, CancellationToken cancellationToken)
    {
        var timeout = _settings.RenderTimeoutSeconds * 1000;
        var playwright = await _playwright.Value;
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true, Args = ["--no-sandbox", "--disable-dev-shm-usage"] }
        );
        var page = await browser.NewPageAsync();
        page.SetDefaultTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await page.SetContentAsync(
                html,
                new PageSetContentOptions { WaitUntil = WaitUntilState.Load, Timeout = timeout }
            );
        }
        catch (TimeoutException ex)
        {
            var title = await page.TitleAsync();
            var body = await page.Locator("body").InnerTextAsync(new LocatorInnerTextOptions { Timeout = 1000 });
            throw new TimeoutException(
                $"Timed out rendering print HTML. Title: '{title}'. Body: '{Trim(body)}'",
                ex
            );
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(
            new PagePdfOptions
            {
                PrintBackground = true,
                PreferCSSPageSize = true,
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

    static string Trim(string value)
    {
        const int maxLength = 500;
        var trimmed = value.ReplaceLineEndings(" ").Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}...";
    }
}
