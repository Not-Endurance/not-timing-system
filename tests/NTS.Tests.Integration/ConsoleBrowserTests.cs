using System.Text.RegularExpressions;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The Console in a real browser against the Api that serves it (#646, ADR-0011): its pages are an assembly that the app
/// fetches when a signed-in person goes to them, so a phone that follows an Event never downloads it.
/// </summary>
public sealed class ConsoleBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Console_is_fetched_when_a_signed_in_person_goes_to_it_and_not_when_a_viewer_opens_the_app()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var fetched = new List<string>();
        page.Request += (_, request) =>
        {
            if (
                request.Url.Contains("NoTiming.Console.", StringComparison.Ordinal)
                && request.Url.EndsWith(".wasm", StringComparison.Ordinal)
            )
            {
                lock (fetched)
                {
                    fetched.Add(request.Url);
                }
            }
        };

        await page.GotoAsync(device.Url($"/events/{scene.EventId}/startlist"));
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        lock (fetched)
        {
            Assert.Empty(fetched); // what a viewer downloads has no Console in it (its symbols are a debug build's, not the assembly)
        }

        await page.GotoAsync(device.Url("/sign-in"));
        await SignInOnTheHostPagesAsync(_host, page, scene.Official.Email);
        await page.WaitForURLAsync(new Regex(@"^[^?#]*/$"), new() { Timeout = PATIENCE });
        await page.EvaluateAsync("Blazor.navigateTo('/console')");

        await Expect(page.GetByText("Console", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        lock (fetched)
        {
            Assert.Single(fetched); // and it is fetched, once, when the person goes to its pages
        }
    }
}
