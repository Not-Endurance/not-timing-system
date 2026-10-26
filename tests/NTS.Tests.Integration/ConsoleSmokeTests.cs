using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The whole way, in real browsers, one for each person (#646): a Tenant Root makes an Event and assigns its Console Operator, the
/// Console Operator starts it, an Official sends a Snapshot from the page of the Snapshots, the Console and a viewer both show it,
/// and the Console Operator prints a Handout. What each page does is tried on its own in the tests of the pages; this one is that
/// they are the same Event, in the same app, over the Api that serves it.
/// </summary>
public sealed class ConsoleSmokeTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const string EVENT_NAME = "The Smoke Cup";
    const int NUMBER = 101; // the Combination of the Setup that is made whole

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleSmokeTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_Tenant_Root_makes_an_Event_the_Console_Operator_starts_it_an_Official_sends_a_Snapshot_which_the_Console_and_a_viewer_show_and_the_Console_Operator_prints_a_Handout()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var root = await TenancySeed.AccountAsync(mongo, tenant, TenancySeed.TenantRootOf(tenant), name: "Rada Root");
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var official = await TenancySeed.AccountAsync(mongo, tenant, name: "Elena Petrova");

        // a Tenant Root makes an Event, and assigns a Console Operator to it, who runs it beside Root
        await using var rootDevice = await _browsers.OpenAsync(_host.BaseAddress);
        var rootPage = rootDevice.Page;
        rootPage.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, rootDevice, root.Email, "/console");
        await rootPage.GetByRole(AriaRole.Button, new() { Name = "New event" }).ClickAsync();
        await rootPage.GetByLabel("Name", new() { Exact = true }).FillAsync(EVENT_NAME);
        await rootPage.GetByLabel("Location", new() { Exact = true }).FillAsync("Kaspichan");
        await rootPage.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        var made = rootPage.GetByRole(AriaRole.Row).Filter(new() { HasText = EVENT_NAME });
        await Expect(made).ToBeVisibleAsync(Loaded());
        await made.GetByRole(AriaRole.Button, new() { Name = "Console Operators" }).ClickAsync();
        var assigning = rootPage.GetByRole(AriaRole.Dialog);
        await Expect(assigning.GetByTestId("console-operators-empty")).ToBeVisibleAsync(Loaded()); // nobody is assigned when an Event is made
        await assigning.GetByLabel("Find an account by name").FillAsync("Boris Iv");
        await rootPage
            .Locator(".mud-popover-open .mud-list-item")
            .Filter(new() { HasText = "Boris Ivanov" })
            .First.ClickAsync();
        await assigning.GetByRole(AriaRole.Button, new() { Name = "Assign", Exact = true }).ClickAsync();
        await Expect(assigning.GetByTestId("console-operators-list").GetByText("Boris Ivanov"))
            .ToBeVisibleAsync(Loaded());
        await assigning.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        // the Console Operator configures it, which is tried on the pages of the Setup, and the Official is given the Event
        var eventId = (await SetupOfAsync(mongo))!["_id"].AsGuid;
        await Eventually(async () => await IsConsoleOperatorAsync(mongo, eventId, consoleOperator.Id));
        await EventSeed.MakeWholeAsync(mongo, tenant, eventId, EVENT_NAME, StartOfTheDay());
        await EventSeed.GrantAsync(mongo, tenant, eventId, "Official", "Steward", official.Email, official.Id);

        // the Console Operator starts it and is taken to its dashboard
        await using var operatorDevice = await _browsers.OpenAsync(_host.BaseAddress);
        var operatorPage = operatorDevice.Page;
        operatorPage.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, operatorDevice, consoleOperator.Email, "/console");
        var theirs = operatorPage.GetByRole(AriaRole.Row).Filter(new() { HasText = EVENT_NAME });
        await theirs.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true }).ClickAsync();
        await operatorPage.WaitForURLAsync(
            operatorDevice.Url($"/console/events/{eventId}/dashboard"),
            new() { Timeout = PATIENCE }
        );
        await Expect(ChipOf(operatorPage, NUMBER)).ToBeVisibleAsync(Loaded());
        await ChipOf(operatorPage, NUMBER).ClickAsync();
        var times = operatorPage.GetByText(new Regex(@"^\d{2}:\d{2}:\d{2}$"));
        await Expect(times.First).ToBeVisibleAsync(Loaded()); // the start of the Participation
        var before = await times.CountAsync();

        // a viewer follows the Event without signing in: the rider is on the way, and has not arrived
        await using var viewerDevice = await _browsers.OpenAsync(_host.BaseAddress);
        var viewer = viewerDevice.Page;
        viewer.SetDefaultTimeout(PATIENCE);
        await viewer.GotoAsync(viewerDevice.Url($"/events/{eventId}/arrivelist"));
        await Expect(viewer.GetByText("Ivan Petrov").First).ToBeVisibleAsync(Loaded());

        // an Official sends the Arrival of the rider from the page of the Snapshots
        await using var officialDevice = await _browsers.OpenAsync(_host.BaseAddress);
        var officialPage = officialDevice.Page;
        officialPage.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, officialDevice, official.Email, $"/events/{eventId}/snapshot");
        await Expect(ChipOf(officialPage, NUMBER)).ToBeVisibleAsync(Loaded());
        await SendSnapshotOfAsync(officialPage, NUMBER);
        await Eventually(async () => await ArrivedAsync(mongo, eventId));

        // the Console shows it, on the page nobody touched, and the viewer's list has lost the rider and the next one has it
        await Eventually(async () => await times.CountAsync() > before);
        await Expect(viewer.GetByText("Ivan Petrov")).ToHaveCountAsync(0, Counted());
        await viewer.EvaluateAsync($"Blazor.navigateTo('/events/{eventId}/presentlist')");
        await Expect(viewer.GetByText("Ivan Petrov").First).ToBeVisibleAsync(Loaded());

        // the Console Operator makes the Handout of the rider, prints it and says that it was printed
        var pages = operatorPage.GetByTestId("console-event-navigation");
        await pages.GetByRole(AriaRole.Button, new() { Name = "Handouts", Exact = true }).ClickAsync();
        var handouts = operatorPage.GetByTestId("console-handouts-panel");
        await handouts.GetByLabel("Combination").FillAsync("Ivan");
        await operatorPage
            .Locator(".mud-popover-open .mud-list-item")
            .Filter(new() { HasText = "Ivan Petrov" })
            .First.ClickAsync();
        await handouts.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(operatorPage.GetByText("Ivan Petrov").First).ToBeVisibleAsync(Loaded());
        await handouts.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => operatorPage.Frames.Any(x => x != operatorPage.MainFrame));
        var printed = operatorPage.Frames.First(x => x != operatorPage.MainFrame);
        await Eventually(async () => (await printed.ContentAsync()).Contains("Ivan Petrov", StringComparison.Ordinal));
        await operatorPage
            .GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Yes", Exact = true })
            .ClickAsync();
        await Eventually(async () => await HandoutsOfAsync(mongo, eventId) == 0);
    }

    /// <summary>
    /// The start of the competition: a minute into the day of the clock of the host, which the Event is Live until the end of. It is a
    /// day of UTC, as the Api keeps the days of an Event, so that it is the same day whichever time zone the machine is in.
    /// </summary>
    static DateTimeOffset StartOfTheDay()
    {
        return new DateTimeOffset(DateTime.UtcNow.Date.AddMinutes(1), TimeSpan.Zero);
    }

    static async Task<BsonDocument?> SetupOfAsync(string mongo)
    {
        return await EventSeed.Setups(mongo).Find(new BsonDocument("Name", EVENT_NAME)).FirstOrDefaultAsync();
    }

    static async Task<bool> IsConsoleOperatorAsync(string mongo, Guid eventId, Guid account)
    {
        return await EventSeed
                .Grants(mongo)
                .CountDocumentsAsync(
                    new BsonDocument
                    {
                        { "EventId", EventSeed.Binary(eventId) },
                        { "Kind", "ConsoleOperator" },
                        { "AccountId", EventSeed.Binary(account) },
                    }
                ) == 1;
    }

    static async Task<bool> ArrivedAsync(string mongo, Guid eventId)
    {
        var stored = await RegistrySeed
            .Collection(mongo, "event_participations")
            .Find(new BsonDocument("EventId", EventSeed.Binary(eventId)))
            .FirstOrDefaultAsync();
        return stored != null
            && stored["Phases"]
                [0]["Events"]
                .AsBsonArray.Any(x => x.ToString()!.Contains("Arrive", StringComparison.Ordinal));
    }

    static async Task<long> HandoutsOfAsync(string mongo, Guid eventId)
    {
        return await EventSeed.CountOfAsync(mongo, "event_handouts", eventId);
    }
}
