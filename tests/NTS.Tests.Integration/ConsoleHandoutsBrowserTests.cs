using Microsoft.Playwright;
using MongoDB.Driver;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;
using Competition = NTS.Domain.Core.Aggregates.Participations.Objects.Competition;

namespace NTS.Tests.Integration;

/// <summary>
/// The Handouts of a Live Event in the Console, in a real browser (#646): printed through the browser and removed once the
/// person says they were printed, made by hand, and made by the Console when a Phase completes on the dashboard.
/// </summary>
public sealed class ConsoleHandoutsBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleHandoutsBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Handouts_are_printed_through_the_browser_and_removed_once_the_person_says_they_were_printed()
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString, arrived: true, handouts: true);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/handouts");

        // the sheets of the Participations, as the Participations are
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Athlete 2").First).ToBeVisibleAsync();

        // printing hands the browser the sheets in a page of its own, and the person is asked whether they were printed
        var panel = page.GetByTestId("console-handouts-panel");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => page.Frames.Any(x => x != page.MainFrame));
        var printed = page.Frames.First(x => x != page.MainFrame);
        await Eventually(async () => (await printed.ContentAsync()).Contains("Athlete 2", StringComparison.Ordinal));
        Assert.Contains("Athlete 1", await printed.ContentAsync());
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByText("Print confirmation")).ToBeVisibleAsync();

        // no is that they were not: they stay
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());
        Assert.Equal(2, (await scene.HandoutsStoredAsync()).Count);

        // yes is that they were: they are removed from the Api and from the page
        await panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Yes", Exact = true })
            .ClickAsync();
        await Eventually(async () => (await scene.HandoutsStoredAsync()).Count == 0);
        await Expect(page.GetByText("Create Handouts manually to print...").First).ToBeVisibleAsync();
        await Expect(page.GetByText("Athlete 1", new() { Exact = false })).ToHaveCountAsync(0, Counted());
    }

    [Fact]
    public async Task A_Handout_is_made_by_hand_and_by_the_dashboard_when_a_Phase_completes_and_the_page_lists_both()
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString, arrived: false, handouts: false);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/dashboard");

        // the first Phase of the first Participation completes: the Arrival and the Presentation are recorded
        await Expect(ChipOf(page, 1)).ToBeVisibleAsync(Loaded());
        await ChipOf(page, 1).ClickAsync();
        await RecordAsync(page, "Arrive", "090000");
        await Expect(page.GetByText("09:00:00", new() { Exact = true }).First).ToBeVisibleAsync();
        await RecordAsync(page, "Present", "091500");
        await Expect(page.GetByText("A Handout was made for #1.")).ToBeVisibleAsync();
        await Eventually(async () => (await scene.HandoutsStoredAsync()).Count == 1);

        // the Handouts page lists it, and the person makes the one of the second Participation by hand
        await page.EvaluateAsync($"Blazor.navigateTo('/console/events/{scene.EventId}/handouts')");
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Athlete 2")).ToHaveCountAsync(0, Counted());
        var panel = page.GetByTestId("console-handouts-panel");
        await panel.GetByLabel("Combination").FillAsync("Athlete 2");
        await page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new() { HasText = "Athlete 2" })
            .First.ClickAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Athlete 2").First).ToBeVisibleAsync();
        await Eventually(async () => (await scene.HandoutsStoredAsync()).Count == 2);
        Assert.Equal(
            [scene.Ids[0], scene.Ids[1]],
            (await scene.HandoutsStoredAsync()).Select(x => x["_id"].AsGuid).OrderBy(x => Array.IndexOf(scene.Ids, x))
        );
    }

    static async Task RecordAsync(IPage page, string button, string digits)
    {
        var panel = page.GetByTestId("console-time-panel");
        var time = panel.GetByLabel("Time");
        await time.ClickAsync();
        await time.PressAsync("Control+A"); // over the 00:00:00 that the field starts with
        await time.PressSequentiallyAsync(digits);
        await panel.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
    }

    sealed class Scene
    {
        public static async Task<Scene> CreateAsync(string mongo, bool arrived, bool handouts)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var eventId = await EventSeed.LiveAsync(mongo, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
            var start = new DateTimeOffset(DateTime.Today.AddHours(6));
            var ids = new List<Guid>();
            foreach (var number in new[] { 1, 2 })
            {
                var participation = TwoPhases(eventId, number, start, arrived);
                await EventSeed.ParticipationAsync(mongo, tenant, participation);
                ids.Add(participation.Id);
                if (handouts)
                {
                    await EventSeed.HandoutAsync(
                        mongo,
                        tenant,
                        new Handout(eventId, participation.Id, participation.Id)
                    );
                }
            }

            return new Scene(mongo, eventId, mainOperator.Email, [.. ids]);
        }

        readonly string _mongo;

        Scene(string mongo, Guid eventId, string email, Guid[] ids)
        {
            _mongo = mongo;
            EventId = eventId;
            Email = email;
            Ids = ids;
        }

        public Guid EventId { get; }
        public string Email { get; }
        public Guid[] Ids { get; }

        public async Task<List<MongoDB.Bson.BsonDocument>> HandoutsStoredAsync()
        {
            return await RegistrySeed
                .Collection(_mongo, "event_handouts")
                .Find(new MongoDB.Bson.BsonDocument("EventId", EventSeed.Binary(EventId)))
                .ToListAsync();
        }

        static Participation TwoPhases(Guid eventId, int number, DateTimeOffset start, bool arrived)
        {
            var country = new Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
            var athlete = new Athlete(
                $"Athlete {number}",
                null,
                country,
                null,
                (100000 + number).ToString(),
                Guid.NewGuid()
            );
            var horse = new Horse($"Horse {number}", null, (200000 + number).ToString(), Guid.NewGuid());
            var combination = new Combination(number, athlete, horse, null, "80", null, null, Guid.NewGuid());
            var first = new Phase(
                "GATE1/40",
                40,
                40,
                null,
                CompetitionRuleset.FEI,
                false,
                null,
                Timestamp.Create(start),
                arrived ? Timestamp.Create(start.AddHours(3)) : null,
                arrived ? Timestamp.Create(start.AddHours(3.25)) : null,
                null,
                false,
                false,
                false
            );
            var last = new Phase(
                "GATE2/40",
                40,
                40,
                null,
                CompetitionRuleset.FEI,
                true,
                null,
                arrived ? Timestamp.Create(start.AddHours(3.25)) : null,
                null,
                null,
                null,
                false,
                false,
                false
            );
            return new Participation(
                ParticipationCategory.Senior,
                new Competition("CEI 1*", CompetitionRuleset.FEI),
                combination,
                new PhaseCollection([first, last]),
                null,
                eventId,
                Guid.NewGuid()
            );
        }
    }
}
