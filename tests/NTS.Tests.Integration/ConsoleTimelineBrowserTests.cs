using System.Globalization;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The timeline of a Participation in the Console, in a real browser (#619, ADR-0005): a dialog opened from the dashboard that
/// lists every time event with who recorded it and when. An event that was accepted is grayed out when it is pointed at and
/// says "Click to disable", one that was disabled says "Click to enable", and one the rules rejected says why and does nothing.
/// A click stages the change and a second one takes it back, and nothing is applied until Save, which is off while nothing is
/// staged and writes the whole choice as one change; closing without it changes nothing. A change the Participation refuses is
/// said in the dialog, with the events named, and the dialog stays open.
/// </summary>
public sealed class ConsoleTimelineBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleTimelineBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Main_Operator_opens_the_timeline_and_sees_every_time_event_with_who_recorded_it_and_what_became_of_it()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");

        var dialog = await OpenTimelineAsync(page);

        var entries = dialog.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(4, Counted());
        await Expect(dialog.GetByTestId("timeline-phase").First).ToContainTextAsync("GATE1");
        await Expect(entries.Nth(0)).ToContainTextAsync("Arrive");
        await Expect(entries.Nth(0).GetByTestId("timeline-actor")).ToHaveTextAsync("Olga Official");
        await Expect(entries.Nth(1).GetByTestId("timeline-outcome")).ToContainTextAsync("Rejected Duplicate Arrive");
        await Expect(entries.Nth(2)).ToContainTextAsync("Arrive (update)");
        await Expect(entries.Nth(2).GetByTestId("timeline-shown")).ToHaveCountAsync(1, Counted()); // the time the Phase shows
        await Expect(entries.Nth(0).GetByTestId("timeline-shown")).ToHaveCountAsync(0, Counted());
        await Expect(entries.Nth(3)).ToContainTextAsync("Present");
        await Expect(entries.Nth(3).GetByTestId("timeline-actor")).ToHaveTextAsync("Boris Ivanov");
        await Expect(entries.Nth(3).GetByTestId("timeline-source")).ToHaveTextAsync("Manual");
        await Expect(entries.Nth(3).GetByTestId("timeline-recorded"))
            .ToHaveTextAsync(
                scene.RecordedAt.ToLocalTime().AddMinutes(3).ToString("HH:mm:ss", CultureInfo.InvariantCulture)
            );
    }

    [Fact]
    public async Task Pointing_at_an_event_says_what_a_click_does_and_an_event_the_rules_rejected_says_why_and_does_nothing()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");
        var dialog = await OpenTimelineAsync(page);
        var entries = dialog.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(4, Counted());

        await entries.Nth(2).HoverAsync();
        await Expect(page.Locator(".mud-tooltip.mud-popover-open")).ToHaveTextAsync("Click to disable");
        await entries.Nth(1).HoverAsync();
        await Expect(page.Locator(".mud-tooltip.mud-popover-open")).ToHaveTextAsync("Rejected Duplicate Arrive");
        await entries.Nth(1).ClickAsync(); // nothing happens to what the rules rejected
        await Expect(entries.Nth(1)).ToHaveAttributeAsync("data-staged", "false");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
        await entries.Nth(2).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());

        dialog = await OpenTimelineAsync(page); // an event that was disabled says it can be enabled again
        entries = dialog.GetByTestId("timeline-entry");
        await entries.Nth(2).HoverAsync();
        await Expect(page.Locator(".mud-tooltip.mud-popover-open")).ToHaveTextAsync("Click to enable");
        await Expect(entries.Nth(2).GetByTestId("timeline-outcome")).ToContainTextAsync("Rejected Manually");
        await Expect(entries.Nth(2).GetByTestId("timeline-outcome")).ToContainTextAsync("Disabled at");
    }

    [Fact]
    public async Task A_click_stages_a_second_click_takes_it_back_and_Save_writes_the_whole_choice_while_closing_writes_nothing()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");
        var dialog = await OpenTimelineAsync(page);
        var entries = dialog.GetByTestId("timeline-entry");
        var save = dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });
        await Expect(entries).ToHaveCountAsync(4, Counted());
        await Expect(save).ToBeDisabledAsync(); // nothing is staged

        await entries.Nth(2).ClickAsync();
        await Expect(entries.Nth(2)).ToHaveAttributeAsync("data-staged", "true");
        await Expect(save).ToBeEnabledAsync();
        await entries.Nth(2).ClickAsync(); // taken back
        await Expect(entries.Nth(2)).ToHaveAttributeAsync("data-staged", "false");
        await Expect(save).ToBeDisabledAsync();

        await entries.Nth(2).ClickAsync(); // several events can be staged
        await entries.Nth(3).FocusAsync(); // by the keyboard as well, as a button is pressed
        await entries.Nth(3).PressAsync("Enter");
        await Expect(entries.Nth(3)).ToHaveAttributeAsync("data-staged", "true");
        await Expect(dialog.GetByTestId("timeline-entry").And(page.Locator("[data-staged=true]")))
            .ToHaveCountAsync(2, Counted());
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());
        Assert.Equal(0, await scene.DisabledEventsAsync()); // closing applied nothing
        dialog = await OpenTimelineAsync(page);
        await Expect(dialog.GetByTestId("timeline-entry").And(page.Locator("[data-staged=true]")))
            .ToHaveCountAsync(0, Counted()); // and left the choice behind

        entries = dialog.GetByTestId("timeline-entry");
        await entries.Nth(2).ClickAsync();
        await entries.Nth(3).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Eventually(async () => await scene.DisabledEventsAsync() == 2);
        var stored = await scene.StoredAsync();
        Assert.Equal(1, stored["Version"].AsInt32); // the whole choice was one write, and the Participation was at 0
        await Expect(page.GetByText(scene.ArriveAfterUpdate, new() { Exact = true })).ToHaveCountAsync(0, Counted()); // the table shows the time it had before the update
        await Expect(page.GetByText(scene.ArriveBeforeUpdate, new() { Exact = true }).First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_change_that_would_leave_an_earlier_Phase_incomplete_while_a_later_one_has_times_is_said_with_the_events_named_and_the_dialog_stays_open()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString, laterPhaseHasATime: true);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");
        var dialog = await OpenTimelineAsync(page);
        var entries = dialog.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(5, Counted());

        await entries.Nth(3).ClickAsync(); // the Presentation of the first Phase
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        var refusal = dialog.GetByTestId("timeline-refusal");
        await Expect(refusal).ToBeVisibleAsync(Loaded());
        await Expect(refusal).ToContainTextAsync("Present");
        await Expect(refusal).ToContainTextAsync("would leave an earlier Phase");
        await Expect(dialog).ToBeVisibleAsync(); // it stays open
        Assert.Equal(0, await scene.DisabledEventsAsync()); // and nothing was applied
        await Expect(entries.Nth(3)).ToHaveAttributeAsync("data-staged", "true"); // what was chosen is still chosen
    }

    [Fact]
    public async Task A_save_over_a_Participation_that_changed_meanwhile_is_refused_the_choice_is_kept_and_saved_over_what_is_there_now()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");
        var dialog = await OpenTimelineAsync(page);
        var entries = dialog.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(4, Counted());
        await entries.Nth(2).ClickAsync();
        await scene.WriteBehindTheBackOfTheTabsAsync(); // the version moves on and no tab is told

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(dialog.GetByTestId("timeline-refusal")).ToContainTextAsync("was changed meanwhile");
        await Expect(entries.Nth(2)).ToHaveAttributeAsync("data-staged", "true", new() { Timeout = PATIENCE });
        Assert.Equal(0, await scene.DisabledEventsAsync());

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Eventually(async () => await scene.DisabledEventsAsync() == 1);
    }

    [Fact]
    public async Task A_choice_that_the_Participation_can_no_longer_take_is_dropped_when_the_save_is_refused_and_is_not_turned_into_the_opposite_one()
    {
        var scene = await TimelineScene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.MainOperator.Email, $"/console/events/{scene.EventId}/dashboard");
        var dialog = await OpenTimelineAsync(page);
        var entries = dialog.GetByTestId("timeline-entry");
        await Expect(entries).ToHaveCountAsync(4, Counted());
        await entries.Nth(2).ClickAsync(); // to disable the update of the arrival
        await scene.DisableBehindTheBackOfTheTabsAsync(2); // and somebody else has disabled it already

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        await Expect(dialog.GetByTestId("timeline-refusal")).ToContainTextAsync("was changed meanwhile");
        await Expect(entries.Nth(2)).ToHaveAttributeAsync("data-staged", "false", new() { Timeout = PATIENCE }); // the choice is not turned into an enabling
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
        Assert.Equal(1, await scene.DisabledEventsAsync()); // it is as the other person left it
    }

    [Fact]
    public async Task A_Participation_that_no_time_was_recorded_for_says_so_and_has_nothing_to_save()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        await EventSeed.ParticipationAsync(
            mongo,
            tenant,
            IntegrationPayloadFactory.ActiveParticipation(
                eventId,
                1,
                Guid.NewGuid(),
                startTime: new DateTimeOffset(DateTime.Today.AddHours(6))
            )
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator.Email, $"/console/events/{eventId}/dashboard");

        var dialog = await OpenTimelineAsync(page);

        await Expect(dialog.GetByTestId("timeline-empty")).ToBeVisibleAsync(Loaded());
        await Expect(dialog.GetByTestId("timeline-entry")).ToHaveCountAsync(0, Counted());
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })).ToBeDisabledAsync();
    }

    async Task<ILocator> OpenTimelineAsync(IPage page)
    {
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await page.GetByTestId("dashboard-timeline").ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync(Loaded());
        return dialog;
    }

    /// <summary>
    /// A Live Event with one Participation whose first Phase has an arrival that an Official recorded, a second arrival that was
    /// refused, an update of the first, and a presentation that the Main Operator recorded; and, when asked for, an arrival in the
    /// second Phase.
    /// </summary>
    sealed class TimelineScene
    {
        static readonly DateTimeOffset START = new(DateTime.Today.AddHours(6));
        static readonly DateTimeOffset ARRIVE = START.AddMinutes(30);

        public static async Task<TimelineScene> CreateAsync(string mongo, bool laterPhaseHasATime = false)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var official = await TenancySeed.AccountAsync(mongo, tenant, name: "Olga Official");
            var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
            var recorded = new DateTimeOffset(DateTime.Today.AddHours(12));
            var participation = IntegrationPayloadFactory.TwoPhaseParticipation(
                eventId,
                1,
                Guid.NewGuid(),
                startTime: START
            );
            var arrived = participation.Process(
                IntegrationPayloadFactory.ArriveSnapshot(1, ARRIVE),
                official.Id,
                recorded
            );
            participation.Process(
                IntegrationPayloadFactory.ArriveSnapshot(1, ARRIVE.AddMinutes(1)),
                official.Id,
                recorded.AddMinutes(1)
            );
            participation.UpdateSnapshot(
                arrived.Id,
                new Timestamp(ARRIVE.AddMinutes(3)),
                official.Id,
                recorded.AddMinutes(2)
            );
            participation.Process(
                IntegrationPayloadFactory.PresentSnapshot(1, START.AddMinutes(40)),
                mainOperator.Id,
                recorded.AddMinutes(3)
            );
            if (laterPhaseHasATime)
            {
                var next = participation.Phases[1].StartTime!.ToDateTimeOffset();
                participation.Process(
                    IntegrationPayloadFactory.ArriveSnapshot(1, next.AddMinutes(20)),
                    official.Id,
                    recorded.AddMinutes(4)
                );
            }

            await EventSeed.ParticipationAsync(mongo, tenant, participation);
            return new TimelineScene(mongo, mainOperator, eventId, participation.Id, recorded);
        }

        readonly string _mongo;
        readonly Guid _participation;

        TimelineScene(
            string mongo,
            TenancySeed.Seeded mainOperator,
            Guid eventId,
            Guid participation,
            DateTimeOffset recordedAt
        )
        {
            _mongo = mongo;
            _participation = participation;
            MainOperator = mainOperator;
            EventId = eventId;
            RecordedAt = recordedAt;
        }

        public TenancySeed.Seeded MainOperator { get; }
        public Guid EventId { get; }
        public DateTimeOffset RecordedAt { get; }
        public string ArriveBeforeUpdate => ARRIVE.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        public string ArriveAfterUpdate =>
            ARRIVE.AddMinutes(3).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        public async Task<BsonDocument> StoredAsync()
        {
            return (await RegistrySeed.StoredAsync(_mongo, "event_participations", _participation))!;
        }

        /// <summary>How many of the time events of the Participation are disabled.</summary>
        public async Task<int> DisabledEventsAsync()
        {
            var stored = await StoredAsync();
            return stored["Phases"]
                .AsBsonArray.SelectMany(x => x["Events"].AsBsonArray)
                .Count(x => x["Outcome"].AsString == "RejectedManually");
        }

        /// <summary>Another person's disabling of the event with the index in the first Phase, which no tab is told of: the version moves on too.</summary>
        public async Task DisableBehindTheBackOfTheTabsAsync(int index)
        {
            await RegistrySeed
                .Collection(_mongo, "event_participations")
                .UpdateOneAsync(
                    new BsonDocument("_id", RegistrySeed.Binary(_participation)),
                    new BsonDocument
                    {
                        { "$set", new BsonDocument($"Phases.0.Events.{index}.Outcome", "RejectedManually") },
                        { "$inc", new BsonDocument("Version", 1) },
                    }
                );
        }

        /// <summary>A write that no tab is told of: the version of the Participation moves on and nothing is announced.</summary>
        public async Task WriteBehindTheBackOfTheTabsAsync()
        {
            await RegistrySeed
                .Collection(_mongo, "event_participations")
                .UpdateOneAsync(
                    new BsonDocument("_id", RegistrySeed.Binary(_participation)),
                    new BsonDocument("$inc", new BsonDocument("Version", 1))
                );
        }
    }
}
