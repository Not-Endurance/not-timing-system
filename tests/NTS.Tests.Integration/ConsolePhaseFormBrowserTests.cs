using Microsoft.Playwright;
using MongoDB.Bson;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The form of a Phase in the Console, in a real browser (#620, ADR-0005): the Console Operator opens it from the heading of the
/// Phase in the table of the dashboard, types the times, and one Save records one edit that lists the times it changed, a time
/// it cleared among them; the timeline shows the edit as one entry and disabling it undoes the whole edit. A time that is out
/// of order is said in the form and nothing is recorded, and a Save that changes nothing writes nothing.
/// </summary>
public sealed class ConsolePhaseFormBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsolePhaseFormBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Console_Operator_edits_times_in_the_form_and_the_timeline_shows_one_edit_whose_disabling_undoes_it_whole()
    {
        var scene = await FormScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(
            _host,
            device,
            scene.ConsoleOperator.Email,
            $"/console/events/{scene.EventId}/dashboard"
        );
        var dialog = await OpenFormAsync(page);
        await Expect(dialog.GetByLabel("Arrive", new() { Exact = true })).ToHaveValueAsync("06:30:00", Filled());
        await Expect(dialog.GetByLabel("Present", new() { Exact = true })).ToHaveValueAsync("06:40:00", Filled());

        await TypeAsync(dialog.GetByLabel("Arrive", new() { Exact = true }), "063200");
        await ClearAsync(dialog.GetByLabel("Present", new() { Exact = true }));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByText("06:32:00", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("06:30:00", new() { Exact = true })).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByText("06:40:00", new() { Exact = true })).ToHaveCountAsync(0, Counted()); // it was cleared
        var events = await scene.EventsAsync();
        Assert.Equal(["Arrived", "Presented", "PhaseUpdated"], events.Select(x => x["Kind"].AsString));
        var listed = events[2]["Changes"].AsBsonArray.Select(x => x.AsBsonDocument).ToList();
        Assert.Equal(["Arrive", "Present"], listed.Select(x => x["Slot"].AsString)); // only the two it changed
        Assert.True(!listed[1].Contains("Time") || listed[1]["Time"].IsBsonNull); // the cleared one says there is none

        var timeline = await OpenTimelineAsync(page);
        var entries = timeline.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(3, Counted());
        await Expect(entries.Nth(2)).ToContainTextAsync("Phase edit");
        await Expect(entries.Nth(2).GetByTestId("timeline-time")).ToHaveTextAsync("Arrive: 06:32:00, Present: none");
        await Expect(entries.Nth(2).GetByTestId("timeline-actor")).ToHaveTextAsync("Boris Ivanov");
        await Expect(entries.Nth(2).GetByTestId("timeline-shown")).ToHaveCountAsync(1, Counted());
        await Expect(entries.Nth(0).GetByTestId("timeline-shown")).ToHaveCountAsync(0, Counted());
        await entries.Nth(2).ClickAsync();
        await timeline.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(timeline).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByText("06:30:00", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("06:40:00", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("06:32:00", new() { Exact = true })).ToHaveCountAsync(0, Counted());
        events = await scene.EventsAsync();
        Assert.Equal("RejectedManually", events[2]["Outcome"].AsString);
    }

    [Fact]
    public async Task A_time_out_of_order_is_said_in_the_form_which_stays_open_and_nothing_is_recorded()
    {
        var scene = await FormScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(
            _host,
            device,
            scene.ConsoleOperator.Email,
            $"/console/events/{scene.EventId}/dashboard"
        );
        var dialog = await OpenFormAsync(page);
        var arrive = dialog.GetByLabel("Arrive", new() { Exact = true });
        await Expect(arrive).ToHaveValueAsync("06:30:00", Filled());

        await TypeAsync(arrive, "053000"); // before the Start at six
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(dialog.GetByText("cannot be sooner than")).ToBeVisibleAsync(Loaded());
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(arrive).ToHaveValueAsync("05:30:00"); // what was typed is kept
        Assert.Equal(2, (await scene.EventsAsync()).Count);
        Assert.Equal(0, await scene.VersionAsync());
    }

    [Fact]
    public async Task A_Save_that_changes_nothing_writes_nothing()
    {
        var scene = await FormScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(
            _host,
            device,
            scene.ConsoleOperator.Email,
            $"/console/events/{scene.EventId}/dashboard"
        );
        var dialog = await OpenFormAsync(page);
        await Expect(dialog.GetByLabel("Arrive", new() { Exact = true })).ToHaveValueAsync("06:30:00", Filled());

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        Assert.Equal(2, (await scene.EventsAsync()).Count);
        Assert.Equal(0, await scene.VersionAsync());
    }

    [Fact]
    public async Task The_Start_typed_in_the_form_is_edited_in_place_and_no_event_records_it()
    {
        var scene = await FormScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(
            _host,
            device,
            scene.ConsoleOperator.Email,
            $"/console/events/{scene.EventId}/dashboard"
        );
        var dialog = await OpenFormAsync(page);
        var start = dialog.GetByLabel("Start", new() { Exact = true });
        await Expect(start).ToHaveValueAsync("06:00:00", Filled());

        await TypeAsync(start, "055000");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Eventually(async () => await scene.VersionAsync() == 1);
        Assert.Equal(2, (await scene.EventsAsync()).Count);
        await Expect(page.GetByText("05:50:00", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
    }

    static async Task<ILocator> OpenFormAsync(IPage page)
    {
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await page.GetByText("GATE1/20").First.ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync(Loaded());
        return dialog;
    }

    static async Task<ILocator> OpenTimelineAsync(IPage page)
    {
        await page.GetByTestId("dashboard-timeline").ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync(Loaded());
        return dialog;
    }

    /// <summary>Types the digits of a time over what the field holds: the mask puts the colons in.</summary>
    static async Task TypeAsync(ILocator field, string digits)
    {
        await field.ClickAsync();
        await field.PressAsync("Control+A");
        await field.PressSequentiallyAsync(digits);
    }

    static async Task ClearAsync(ILocator field)
    {
        await field.ClickAsync();
        await field.PressAsync("Control+A");
        await field.PressAsync("Backspace");
        await Expect(field).ToHaveValueAsync(string.Empty);
    }

    /// <summary>
    /// A Live Event with one Participation of two Phases whose first one was arrived at (06:30) and presented (06:40) by an
    /// Official, and the Console Operator who runs it.
    /// </summary>
    sealed class FormScene
    {
        static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6));

        public static async Task<FormScene> CreateAsync(string mongo)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var official = await TenancySeed.AccountAsync(mongo, tenant, name: "Olga Official");
            var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
            var recorded = new DateTimeOffset(DateTime.Today.AddHours(12));
            var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
                eventId,
                1,
                Guid.NewGuid(),
                startTime: START
            );
            participation.Process(
                IntegrationPayloadFactory.ArriveSnapshot(1, START.AddMinutes(30)),
                official.Id,
                recorded
            );
            participation.Process(
                IntegrationPayloadFactory.PresentSnapshot(1, START.AddMinutes(40)),
                official.Id,
                recorded.AddMinutes(1)
            );
            await EventSeed.ParticipationAsync(mongo, tenant, participation);
            return new FormScene(mongo, consoleOperator, eventId, participation.Id);
        }

        readonly string _mongo;
        readonly Guid _participation;

        FormScene(string mongo, TenancySeed.Seeded consoleOperator, Guid eventId, Guid participation)
        {
            _mongo = mongo;
            _participation = participation;
            ConsoleOperator = consoleOperator;
            EventId = eventId;
        }

        public TenancySeed.Seeded ConsoleOperator { get; }
        public Guid EventId { get; }

        public async Task<BsonDocument> StoredAsync()
        {
            return (await RegistrySeed.StoredAsync(_mongo, "event_participations", _participation))!;
        }

        /// <summary>The time events of the first Phase, in the order they were recorded.</summary>
        public async Task<List<BsonDocument>> EventsAsync()
        {
            return [.. (await StoredAsync())["Phases"][0]["Events"].AsBsonArray.Select(x => x.AsBsonDocument)];
        }

        /// <summary>The version of the document; one that no write has counted yet is at 0, which it does not keep.</summary>
        public async Task<int> VersionAsync()
        {
            return (await StoredAsync()).TryGetValue("Version", out var version) ? version.AsInt32 : 0;
        }
    }
}
