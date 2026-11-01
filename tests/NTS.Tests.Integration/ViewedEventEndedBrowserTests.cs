using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Core.Aggregates;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// An Event that ends while a person has it open (#631, ADR-0007), in a real browser: nothing tells the page until the person
/// writes, and the write that the Api refuses because the Event has ended turns the page into the record of the Event where it
/// is. The connection to the Event closes, the controls go, a banner says why, and what the Historic Event shows is shown from
/// the Historic Event; a write that is refused for another reason leaves the page as it is. The History lists the Events that
/// have ended, the last first, and opens one as a record, with no connection and no Snapshot to capture.
/// </summary>
public sealed class ViewedEventEndedBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const string NOT_AVAILABLE = "This page is not available for an Event that has ended.";

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ViewedEventEndedBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Snapshot_page_of_an_Event_that_has_ended_is_the_record_of_the_Event_after_the_Snapshot_that_was_refused_and_so_is_it_when_it_is_loaded_again()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var hub = new Hub(page);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await Eventually(() => Task.FromResult(hub.Open > 0)); // the Event is followed
        await EndAsync(_host.MongoConnectionString, scene.EventId); // and nothing tells the page that it ended
        await Task.Delay(1500);
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(); // the person is not interrupted
        await Expect(page.GetByTestId("event-ended-banner")).ToHaveCountAsync(0, Counted());

        await SendSnapshotOfAsync(page, 1); // until they write

        await Expect(page.GetByTestId("event-ended-banner")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded());
        await Expect(ChipOf(page, 1)).ToHaveCountAsync(0, Counted()); // no control of the Snapshot page is left
        await Eventually(() => Task.FromResult(hub.Open == 0)); // the connection is closed
        Assert.Empty(await scene.EventsOfAsync(1)); // and nothing was recorded
        Assert.Contains("/snapshot", page.Url, StringComparison.Ordinal); // in place: the person was not taken anywhere

        await page.ReloadAsync(); // a reload gives the same result
        await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("event-ended-banner")).ToHaveCountAsync(0, Counted());
        Assert.Equal(0, hub.Open); // and the page that was loaded again is not connected to it
    }

    [Fact]
    public async Task The_page_of_the_address_that_names_no_Event_turns_into_the_record_of_the_Event_the_connection_followed()
    {
        var scene = await UiScene.CreateAsync(_host);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var hub = new Hub(page);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, "/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await Eventually(() => Task.FromResult(hub.Open > 0));
        Assert.EndsWith("/snapshot", page.Url, StringComparison.Ordinal); // the Snapshot page: the Performance page has chips too
        await EndAsync(_host.MongoConnectionString, scene.EventId);

        await SendSnapshotOfAsync(page, 1);

        await Expect(page.GetByTestId("event-ended-banner")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded()); // the Event stays the one the page shows, though the connection that named it is gone
        await Eventually(() => Task.FromResult(hub.Open == 0));
        Assert.EndsWith("/snapshot", page.Url, StringComparison.Ordinal);
        Assert.Empty(await scene.EventsOfAsync(1));
    }

    [Fact]
    public async Task The_banner_is_the_banner_of_the_Event_that_ended_and_not_of_the_Event_the_page_shows_next()
    {
        var scene = await UiScene.CreateAsync(_host);
        var mongo = _host.MongoConnectionString;
        var other = await EventSeed.LiveAsync(mongo, scene.Tenant, null, DateTimeOffset.UtcNow); // another Event that the Official sends to
        await EventSeed.GrantAsync(
            mongo,
            scene.Tenant,
            other,
            "Official",
            "Steward",
            scene.Official.Email,
            scene.Official.Id
        );
        await EventSeed.ParticipationAsync(
            mongo,
            scene.Tenant,
            IntegrationPayloadFactory.ActiveParticipation(
                other,
                1,
                Guid.NewGuid(),
                startTime: DateTimeOffset.Now.AddHours(-1)
            )
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Official.Email, $"/events/{scene.EventId}/snapshot");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await EndAsync(mongo, scene.EventId);
        await SendSnapshotOfAsync(page, 1);
        await Expect(page.GetByTestId("event-ended-banner")).ToBeVisibleAsync(Loaded());

        await page.EvaluateAsync($"Blazor.navigateTo('/events/{other}/snapshot')");

        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded()); // the other Event is running, and its page is the Snapshot page
        await Expect(page.GetByTestId("event-ended-banner")).ToHaveCountAsync(0, Counted());
        await EndAsync(mongo, other); // the tests of the class share one host, and the address that names no Event follows the only Live Event there is
    }

    [Fact]
    public async Task The_pages_that_run_an_Event_say_to_its_Main_Operator_that_the_Event_has_ended_and_not_that_they_may_not_run_it()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.HistoricAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator.Email, $"/console/events/{eventId}/dashboard");

        foreach (var path in new[] { "dashboard", "handouts", "startlist" })
        {
            await page.EvaluateAsync($"Blazor.navigateTo('/console/events/{eventId}/{path}')");

            await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded());
            await Expect(page.GetByText("Only the Main Operator of an Event opens its Setup."))
                .ToHaveCountAsync(0, Counted());
        }
    }

    [Fact]
    public async Task The_dashboard_turns_into_the_record_of_the_Event_after_the_edit_that_was_refused_and_an_edit_refused_for_another_reason_leaves_it_as_it_is()
    {
        var scene = await ConsoleScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var hub = new Hub(page);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/dashboard");
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("console-connection"))
            .ToContainTextAsync("Connected", new() { Timeout = PATIENCE });
        var panel = page.GetByTestId("console-eliminations-panel");
        await scene.WriteBehindTheBackOfTheTabsAsync(1); // the Participation was changed since the page read it

        await panel.GetByText("WD", new() { Exact = true }).ClickAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Eliminate", Exact = true }).ClickAsync();

        await Expect(page.GetByText("was changed meanwhile")).ToBeVisibleAsync(Loaded()); // refused, and not for the end
        await Expect(page.GetByTestId("event-ended-banner")).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByTestId("console-time-panel")).ToBeVisibleAsync();
        Assert.True(hub.Open > 0);
        await EndAsync(_host.MongoConnectionString, scene.EventId);

        await panel.GetByText("WD", new() { Exact = true }).ClickAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Eliminate", Exact = true }).ClickAsync();

        await Expect(page.GetByTestId("event-ended-banner")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("console-time-panel")).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByTestId("console-connection")).ToHaveCountAsync(0, Counted()); // the Console lists it as Historic now
        var navigation = page.GetByTestId("console-event-navigation");
        await Expect(navigation.GetByRole(AriaRole.Button)).ToHaveCountAsync(2, Counted()); // the Console and the Results: nothing of a Live Event
        await Eventually(() => Task.FromResult(hub.Open == 0));
        var stored = await scene.StoredAsync(1);
        Assert.True(!stored.TryGetValue("Eliminated", out var eliminated) || eliminated.IsBsonNull); // nothing was taken
    }

    [Fact]
    public async Task The_Results_are_shown_from_the_Historic_Event_after_the_Ranking_that_was_to_be_removed_was_refused_and_nothing_can_be_changed()
    {
        var scene = await ConsoleScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var hub = new Hub(page);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/rankings");
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded());
        var panel = page.GetByTestId("console-rankings-panel");
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Custom ranking", Exact = true }))
            .ToBeVisibleAsync();
        await EndAsync(_host.MongoConnectionString, scene.EventId);

        await panel
            .Locator(
                "xpath=.//*[normalize-space(text())='Open class']/following::*[contains(@class,'mud-chip-close-button')][1]"
            )
            .ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();

        await Expect(page.GetByTestId("event-ended-banner")).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync(Loaded()); // the Results are the Historic Event's now
        await Expect(panel.GetByText("Open class", new() { Exact = true }).First).ToBeVisibleAsync(); // and so is the Ranking that was refused
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Custom ranking", Exact = true }))
            .ToHaveCountAsync(0, Counted());
        await Expect(panel.Locator(".mud-chip-close-button")).ToHaveCountAsync(0, Counted()); // no Ranking can be removed
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true })).ToBeEnabledAsync();
        await Eventually(() => Task.FromResult(hub.Open == 0));
        Assert.Contains("Open class", await scene.RankingNamesAsync()); // it was not removed
    }

    [Fact]
    public async Task The_History_lists_the_Events_that_have_ended_the_last_first_and_opens_one_as_a_record_with_no_connection_and_no_Snapshot_to_capture()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo);
        var now = DateTimeOffset.UtcNow;
        var middle = await EventSeed.HistoricAsync(mongo, tenant, null, now.AddDays(-5)); // ended six days ago
        var latest = await EventSeed.HistoricAsync(mongo, tenant, null, now.AddDays(-2)); // ended three days ago
        var oldest = await EventSeed.HistoricAsync(mongo, tenant, null, now.AddDays(-9)); // ended ten days ago
        var participations = new[] { IntegrationPayloadFactory.ActiveParticipation(latest, 1, Guid.NewGuid()) };
        await EventSeed.ParticipationAsync(mongo, tenant, participations[0]);
        await EventSeed.RankingAsync(
            mongo,
            tenant,
            IntegrationPayloadFactory.Ranking(latest, participations, Guid.NewGuid(), "Latest Ranking")
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var hub = new Hub(page);

        await page.GotoAsync(device.Url("/historic-events"));

        var name = (Guid id) => $"Event {id.ToString("N")[..6]}";
        await Expect(page.GetByText(name(oldest)).First).ToBeVisibleAsync(Loaded());
        var text = await page.Locator("body").InnerTextAsync();
        var order = new[] { latest, middle, oldest }
            .Select(x => text.IndexOf(name(x), StringComparison.Ordinal))
            .ToArray();
        Assert.All(order, x => Assert.True(x >= 0));
        Assert.Equal(order.Order(), order); // the Event that ended last is the first of the list
        await page.Locator(".border-solid")
            .Filter(new() { HasText = name(latest) })
            .GetByRole(AriaRole.Button)
            .First.ClickAsync();
        await page.WaitForURLAsync(device.Url($"/historic-events/{latest}"), new() { Timeout = PATIENCE });
        await Expect(page.GetByText("Latest Ranking").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Integration Rider").First).ToBeVisibleAsync();

        foreach (var aboutTheRunningEvent in new[] { "startlist", "performance" })
        {
            await page.EvaluateAsync($"Blazor.navigateTo('/events/{latest}/{aboutTheRunningEvent}')");
            await Expect(page.GetByText(NOT_AVAILABLE).First).ToBeVisibleAsync(Loaded()); // nothing of the running Event
            await Expect(ChipOf(page, 1)).ToHaveCountAsync(0, Counted());
        }

        Assert.Equal(0, hub.Opened); // and the hub was never asked for
    }

    static async Task EndAsync(string mongo, Guid eventId)
    {
        await EventSeed
            .Cores(mongo)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(eventId)),
                new BsonDocument(
                    "$set",
                    new BsonDocument
                    {
                        { "StartDay", new BsonDateTime(DateTime.UtcNow.AddDays(-3)) },
                        { "EndDay", new BsonDateTime(DateTime.UtcNow.AddDays(-2)) },
                    }
                )
            );
    }

    /// <summary>The connections to the hub of the Api that the page makes, and which of them are open.</summary>
    sealed class Hub
    {
        readonly List<IWebSocket> _sockets = [];

        public Hub(IPage page)
        {
            page.WebSocket += (_, socket) =>
            {
                if (socket.Url.Contains("/live-hub", StringComparison.Ordinal))
                {
                    lock (_sockets)
                    {
                        _sockets.Add(socket);
                    }
                }
            };
        }

        public int Opened
        {
            get
            {
                lock (_sockets)
                {
                    return _sockets.Count;
                }
            }
        }

        public int Open
        {
            get
            {
                lock (_sockets)
                {
                    return _sockets.Count(x => !x.IsClosed);
                }
            }
        }
    }

    /// <summary>A Live Event of two Participations that have started, its Rankings, and the Main Operator who runs it.</summary>
    sealed class ConsoleScene
    {
        public static async Task<ConsoleScene> CreateAsync(string mongo)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
            var participations = new List<Participation>();
            foreach (var number in new[] { 1, 2 })
            {
                var participation = IntegrationPayloadFactory.ActiveParticipation(
                    eventId,
                    number,
                    Guid.NewGuid(),
                    startTime: new DateTimeOffset(DateTime.Today.AddHours(6))
                );
                await EventSeed.ParticipationAsync(mongo, tenant, participation);
                participations.Add(participation);
            }

            foreach (var name in new[] { "Results of the day", "Open class" })
            {
                await EventSeed.RankingAsync(
                    mongo,
                    tenant,
                    IntegrationPayloadFactory.Ranking(
                        eventId,
                        participations,
                        Guid.Parse("00" + Guid.NewGuid().ToString()[2..]), // the Api lists by id: the first is the one the page starts on
                        name
                    )
                );
            }

            return new ConsoleScene(mongo, mainOperator.Email, eventId, participations.Select(x => x.Id).ToList());
        }

        readonly string _mongo;
        readonly IReadOnlyList<Guid> _participations;

        ConsoleScene(string mongo, string email, Guid eventId, IReadOnlyList<Guid> participations)
        {
            _mongo = mongo;
            _participations = participations;
            Email = email;
            EventId = eventId;
        }

        public string Email { get; }
        public Guid EventId { get; }

        public async Task<BsonDocument> StoredAsync(int number)
        {
            return (await RegistrySeed.StoredAsync(_mongo, "event_participations", _participations[number - 1]))!;
        }

        public async Task<IReadOnlyList<string>> RankingNamesAsync()
        {
            var documents = await RegistrySeed
                .Collection(_mongo, "event_rankings")
                .Find(new BsonDocument("EventId", EventSeed.Binary(EventId)))
                .ToListAsync();
            return [.. documents.Select(x => x["Name"].AsString)];
        }

        /// <summary>A write that no tab is told of: the version of the Participation moves on and nothing is announced.</summary>
        public async Task WriteBehindTheBackOfTheTabsAsync(int number)
        {
            await RegistrySeed
                .Collection(_mongo, "event_participations")
                .UpdateOneAsync(
                    new BsonDocument("_id", RegistrySeed.Binary(_participations[number - 1])),
                    new BsonDocument("$inc", new BsonDocument("Version", 1))
                );
        }
    }
}
