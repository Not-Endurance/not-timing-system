using System.Text.RegularExpressions;
using Microsoft.Playwright;
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

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_and_assigns_its_Main_Operator_who_then_runs_it_from_the_Console()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var root = await TenancySeed.AccountAsync(mongo, tenant, TenancySeed.TenantRootOf(tenant), name: "Rada Root");
        var next = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, root.Email);
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        await page.GetByRole(AriaRole.Button, new() { Name = "New event" }).ClickAsync();
        await page.GetByLabel("Name", new() { Exact = true }).FillAsync("The Cup");
        await page.GetByLabel("Location", new() { Exact = true }).FillAsync("Kaspichan");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();

        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        await Expect(row).ToBeVisibleAsync(Loaded());
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true })).ToBeVisibleAsync(); // it runs it, as its maker
        await row.GetByRole(AriaRole.Button, new() { Name = "Assign the Main Operator" }).ClickAsync();
        await page.GetByLabel("Find an account by name").FillAsync("Boris Iv");
        await page.GetByText("Boris Ivanov").First.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync();

        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true }))
            .ToHaveCountAsync(0, Counted()); // and having assigned it away, it does not
        await device.FetchAsync("DELETE", "/api/sessions/current");
        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, next.Email);
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        var theirs = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        await Expect(theirs.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true }))
            .ToBeVisibleAsync(Loaded());
        await Expect(theirs.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true })).ToBeVisibleAsync();
    }
}
