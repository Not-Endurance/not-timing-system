using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The way about the Console in a real browser (#646): the drawer leads to it, its home to the pages of an Event, and the pages
/// of a Live Event to one another, each offering what the Event at its stage has for the person and no more.
/// </summary>
public sealed class ConsoleNavigationBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleNavigationBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_drawer_leads_to_the_Console_and_the_pages_of_a_Live_Event_to_one_another()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await page.GotoAsync(device.Url("/sign-in"));
        await SignInOnTheHostPagesAsync(_host, page, mainOperator.Email);
        await page.WaitForURLAsync(new Regex(@"^[^?#]*/$"), new() { Timeout = PATIENCE });

        // the drawer of a signed-in person has the Console
        await page.Locator("strong").Filter(new() { HasTextRegex = new Regex("^Console$") }).ClickAsync(); // a link of the drawer has no role of its own
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Dashboard", Exact = true }))
            .ToBeVisibleAsync(Loaded());

        // an Event that the person runs leads on to its dashboard, which has the pages of the Event beside it
        await page.GetByRole(AriaRole.Button, new() { Name = "Dashboard", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url($"/console/events/{eventId}/dashboard"), new() { Timeout = PATIENCE });
        var navigation = page.GetByTestId("console-event-navigation");
        await Expect(navigation.GetByRole(AriaRole.Button, new() { Name = "Dashboard", Exact = true }))
            .ToBeDisabledAsync(Settled());
        await Expect(navigation.GetByRole(AriaRole.Button)).ToHaveCountAsync(8, Counted());
        await Expect(page.GetByTestId("console-connection"))
            .ToContainTextAsync("Connected to", new() { Timeout = PATIENCE }); // the connection to the Event is up, and the person is told
        await navigation.GetByRole(AriaRole.Button, new() { Name = "Handouts", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url($"/console/events/{eventId}/handouts"), new() { Timeout = PATIENCE });
        await Expect(page.GetByText("Create Handouts manually to print...").First).ToBeVisibleAsync(Loaded());
        await Expect(navigation.GetByRole(AriaRole.Button, new() { Name = "Handouts", Exact = true }))
            .ToBeDisabledAsync(Settled());

        await navigation.GetByRole(AriaRole.Button, new() { Name = "Startlist", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url($"/console/events/{eventId}/startlist"), new() { Timeout = PATIENCE });
        await Expect(page.GetByText("No start has a time yet.").First).ToBeVisibleAsync(Loaded());

        await navigation.GetByRole(AriaRole.Button, new() { Name = "Ranklist", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url($"/console/events/{eventId}/rankings"), new() { Timeout = PATIENCE });

        // the viewers' pages are the same app, and the way back is the Console
        await navigation.GetByRole(AriaRole.Button, new() { Name = "Arrivelist", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(device.Url($"/events/{eventId}/arrivelist"), new() { Timeout = PATIENCE });
        await page.GotoAsync(device.Url($"/console/events/{eventId}/rankings"));
        await navigation.GetByRole(AriaRole.Button, new() { Name = "Console", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/console$"), new() { Timeout = PATIENCE });
    }

    [Fact]
    public async Task The_Results_of_an_Event_that_is_over_open_in_the_Console_from_its_home()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.HistoricAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator.Email, "/console");

        await page.GetByRole(AriaRole.Button, new() { Name = "Results", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(device.Url($"/console/events/{eventId}/rankings"), new() { Timeout = PATIENCE });
        var navigation = page.GetByTestId("console-event-navigation");
        await Expect(navigation.GetByRole(AriaRole.Button, new() { Name = "Ranklist", Exact = true }))
            .ToBeDisabledAsync(Settled());
        await Expect(navigation.GetByRole(AriaRole.Button)).ToHaveCountAsync(2, Counted()); // the Console and the Results: nothing of a Live Event
    }

    /// <summary>The time to wait for a button that is to be the one the person is on, which the page that shows it is loaded by when it is.</summary>
    static LocatorAssertionsToBeDisabledOptions Settled()
    {
        return new LocatorAssertionsToBeDisabledOptions { Timeout = PATIENCE };
    }
}
