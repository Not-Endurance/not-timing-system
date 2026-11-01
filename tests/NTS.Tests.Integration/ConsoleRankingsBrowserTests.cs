using System.Text.RegularExpressions;
using System.Xml.Linq;
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
/// The Rankings and Results of an Event in the Console, in a real browser (#646): printed through the browser, exported for the
/// FEI as a file that is downloaded, made new from the Participations the Console Operator chooses, and removed.
/// </summary>
public sealed class ConsoleRankingsBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleRankingsBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task The_Results_of_a_Ranking_are_printed_through_the_browser_and_exported_for_the_FEI_as_a_file()
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/rankings");

        // the Results of the Ranking, with its Participations in the order they were placed
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByText("Athlete 2").First).ToBeVisibleAsync();

        // printing hands the browser the document in a page of its own, which it prints
        var panel = page.GetByTestId("console-rankings-panel");
        await panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => page.Frames.Any(x => x != page.MainFrame));
        var printed = page.Frames.First(x => x != page.MainFrame);
        await Eventually(async () => (await printed.ContentAsync()).Contains("Athlete 2", StringComparison.Ordinal));
        var html = await printed.ContentAsync();
        Assert.Contains("print-document", html);
        Assert.Contains("CEI 1*", html);
        Assert.Contains($"data:image/png;base64,{Convert.ToBase64String(Scene.LOGO)}", html); // with the logo of the Tenant at its head

        // the export is a file the browser downloads, which the FEI takes
        var download = await page.RunAndWaitForDownloadAsync(
            () => panel.GetByRole(AriaRole.Button, new() { Name = "Export FEI", Exact = true }).ClickAsync()
        );
        Assert.StartsWith("fei-export-", download.SuggestedFilename);
        Assert.EndsWith(".xml", download.SuggestedFilename);
        var xml = XDocument.Load((await download.PathAsync())!);
        var ns = xml.Root!.Name.Namespace;
        Assert.Equal(
            "FEI-SHOW-1",
            xml.Root.Element(ns + "EventResult")!.Element(ns + "Show")!.Attribute("FEIID")!.Value
        );
        Assert.Equal(
            ["Athlete", "Athlete"],
            xml.Descendants(ns + "Athlete").Select(x => x.Attribute("FirstName")!.Value)
        );
        Assert.Equal(["1", "2"], xml.Descendants(ns + "Position").Select(x => x.Attribute("Rank")!.Value));
    }

    [Fact]
    public async Task A_Ranking_of_chosen_Participations_is_made_and_removed_while_the_Event_is_Live()
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/rankings");
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());

        async Task CreateAsync(string name, string athlete)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Custom ranking", Exact = true }).ClickAsync();
            var form = page.GetByRole(AriaRole.Dialog);
            await Expect(form).ToBeVisibleAsync(Loaded());
            await form.GetByLabel("Name", new() { Exact = true }).FillAsync(name); // named first: choosing a template keeps it
            await form.GetByLabel("Template ranking").ClickAsync(); // the settings of a Ranking that exists are the start of the new one
            await page.Locator(".mud-popover-open .mud-list-item")
                .Filter(new() { HasText = "CEI 1*" })
                .First.ClickAsync();
            await form.GetByLabel("Select a Participation to add").FillAsync(athlete);
            await page.Locator(".mud-popover-open .mud-list-item")
                .Filter(new() { HasText = athlete })
                .First.ClickAsync();
            await form.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();
            await form.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        }

        await CreateAsync("Open class", "Athlete 2");

        await Eventually(async () => (await scene.RankingsStoredAsync()).Any(x => x.Name == "Open class"));
        await Expect(page.GetByText("Open class", new() { Exact = true }).First).ToBeVisibleAsync();
        var stored = (await scene.RankingsStoredAsync()).Single(x => x.Name == "Open class");
        Assert.Equal(1, stored.Entries);

        // the person chooses it, and it is the one that is shown and printed: the page does not choose another when it is drawn again
        var rankings = page.GetByTestId("console-rankings-panel");
        await rankings
            .Locator("xpath=.//*[normalize-space(text())='Open class']/following::*[contains(@class,'mud-chip')][1]")
            .ClickAsync(); // the chip that follows its name in the menu
        await CreateAsync("Second class", "Athlete 1"); // and the page is drawn again with another Ranking in its menu
        await Eventually(async () => (await scene.RankingsStoredAsync()).Any(x => x.Name == "Second class"));
        await Expect(page.GetByText("Second class", new() { Exact = true }).First).ToBeVisibleAsync();
        await rankings.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true }).ClickAsync();
        await Eventually(async () => page.Frames.Any(x => x != page.MainFrame));
        var printed = page.Frames.First(x => x != page.MainFrame);
        await Eventually(async () => (await printed.ContentAsync()).Contains("Open class", StringComparison.Ordinal));
        Assert.DoesNotContain("Second class", await printed.ContentAsync(), StringComparison.Ordinal);

        // and it is removed, after the person is asked, from the Api and from the page: the chip that follows its name in the menu
        var close = page.GetByTestId("console-rankings-panel")
            .Locator(
                "xpath=.//*[normalize-space(text())='Open class']/following::*[contains(@class,'mud-chip-close-button')][1]"
            );
        await close.ClickAsync();
        await page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })
            .ClickAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0, Counted());
        Assert.Contains(await scene.RankingsStoredAsync(), x => x.Name == "Open class"); // the person gave it up
        await close.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Eventually(async () => (await scene.RankingsStoredAsync()).All(x => x.Name != "Open class"));
        await Expect(page.GetByText("Open class", new() { Exact = true })).ToHaveCountAsync(0, Counted());
        Assert.Contains(await scene.RankingsStoredAsync(), x => x.Name == "CEI 1*"); // the one that was there is still there
    }

    [Fact]
    public async Task The_Results_of_an_Event_that_is_over_offer_no_change_of_its_Rankings()
    {
        var scene = await Scene.CreateAsync(_host.MongoConnectionString, historic: true);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/rankings");

        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        var panel = page.GetByTestId("console-rankings-panel");
        await Expect(panel.GetByText("CEI 1*", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Custom ranking", Exact = true }))
            .ToHaveCountAsync(0, Counted());
        await Expect(panel.Locator(".mud-chip-close-button")).ToHaveCountAsync(0, Counted()); // and none can be removed
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Print", Exact = true })).ToBeEnabledAsync(); // what it was can be printed
    }

    [Fact]
    public async Task The_export_for_the_FEI_is_offered_when_a_Ranking_has_the_settings_of_the_FEI_and_not_when_none_has()
    {
        var some = await Scene.CreateAsync(
            _host.MongoConnectionString,
            false,
            ("Local", Fei.None),
            ("CEI 1*", Fei.All)
        );
        var none = await Scene.CreateAsync(_host.MongoConnectionString, false, ("Local", Fei.None));
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, some.Email, $"/console/events/{some.EventId}/rankings");
        var export = page.GetByTestId("console-rankings-panel")
            .GetByRole(AriaRole.Button, new() { Name = "Export FEI", Exact = true });
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        var one = await export.IsEnabledAsync(); // one of the two Rankings is made for the FEI

        await device.FetchAsync("DELETE", "/api/sessions/current");
        await SignInAndOpenAsync(_host, device, none.Email, $"/console/events/{none.EventId}/rankings");
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());
        var neither = await export.IsEnabledAsync();

        Assert.True(one);
        Assert.False(neither); // and there is nothing to export of what has none of its settings
    }

    [Fact]
    public async Task An_export_that_cannot_be_made_is_said_to_the_person_in_its_words()
    {
        var scene = await Scene.CreateAsync(
            _host.MongoConnectionString,
            false,
            ("CEI 1*", Fei.All),
            ("Half", Fei.Some)
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.Email, $"/console/events/{scene.EventId}/rankings");
        await Expect(page.GetByText("Athlete 1").First).ToBeVisibleAsync(Loaded());

        await page.GetByTestId("console-rankings-panel")
            .GetByRole(AriaRole.Button, new() { Name = "Export FEI", Exact = true })
            .ClickAsync(); // one of the Rankings has some of the settings and not all of them

        await Expect(page.GetByText("missing FEI export configuration").First).ToBeVisibleAsync();
    }

    sealed class Scene
    {
        public static readonly byte[] LOGO = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 6, 7, 8];

        public static async Task<Scene> CreateAsync(
            string mongo,
            bool historic = false,
            params (string Name, Fei Settings)[] rankings
        )
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var eventId = historic
                ? await EventSeed.HistoricAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow)
                : await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
            var arrive = new DateTimeOffset(DateTime.Today.AddHours(11));
            var participations = new[] { Completed(eventId, 1, arrive), Completed(eventId, 2, arrive.AddMinutes(20)) };
            foreach (var participation in participations)
            {
                await EventSeed.ParticipationAsync(mongo, tenant, participation);
            }

            (string Name, Fei Settings)[] made = rankings.Length == 0 ? [("CEI 1*", Fei.All)] : rankings;
            foreach (var (name, settings) in made)
            {
                var ranking = new Ranking(
                    name,
                    CompetitionRuleset.FEI,
                    ParticipationCategory.Senior,
                    settings != Fei.None ? "FEI-EVENT-1" : null,
                    settings == Fei.All ? "CEI1" : null,
                    settings == Fei.All ? "FEI-COMP-1" : null,
                    settings == Fei.All ? "E Comp" : null,
                    settings == Fei.All ? "01" : null,
                    participations.Select((x, index) => new RankingEntry(x.Id, false, index + 1)),
                    eventId,
                    Guid.Parse("00" + Guid.NewGuid().ToString()[2..]) // the Api lists by id: these are the first, so that a Ranking a person makes is never the one the page starts on
                );
                await EventSeed.RankingAsync(mongo, tenant, ranking);
            }

            await RegistrySeed
                .Collection(mongo, "brandings")
                .InsertOneAsync(
                    new MongoDB.Bson.BsonDocument
                    {
                        { "_id", $"tenant:{tenant}" },
                        { "TenantId", tenant },
                        {
                            "Right",
                            new MongoDB.Bson.BsonDocument
                            {
                                { "ContentType", "image/png" },
                                { "Content", new MongoDB.Bson.BsonBinaryData(LOGO) },
                            }
                        },
                    }
                );
            await EventSeed
                .Cores(mongo)
                .UpdateOneAsync(
                    new MongoDB.Bson.BsonDocument("_id", EventSeed.Binary(eventId)),
                    new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("FeiShowId", "FEI-SHOW-1"))
                );
            return new Scene(mongo, eventId, consoleOperator.Email);
        }

        readonly string _mongo;

        Scene(string mongo, Guid eventId, string email)
        {
            _mongo = mongo;
            EventId = eventId;
            Email = email;
        }

        public Guid EventId { get; }
        public string Email { get; }

        public async Task<IReadOnlyList<(string Name, int Entries)>> RankingsStoredAsync()
        {
            var documents = await RegistrySeed
                .Collection(_mongo, "event_rankings")
                .Find(new MongoDB.Bson.BsonDocument("EventId", EventSeed.Binary(EventId)))
                .ToListAsync();
            return [.. documents.Select(x => (x["Name"].AsString, x["Entries"].AsBsonArray.Count))];
        }

        static Participation Completed(Guid eventId, int number, DateTimeOffset arrive)
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
            var combination = new Combination(number, athlete, horse, null, "40", null, null, Guid.NewGuid());
            var phase = new Phase(
                "GATE1/40",
                40,
                40,
                null,
                CompetitionRuleset.FEI,
                true,
                null,
                Timestamp.Create(arrive.AddHours(-3)),
                Timestamp.Create(arrive),
                Timestamp.Create(arrive.AddMinutes(5)),
                null,
                false,
                false,
                false
            );
            return new Participation(
                ParticipationCategory.Senior,
                new Competition("CEI 1*", CompetitionRuleset.FEI),
                combination,
                new PhaseCollection([phase]),
                null,
                eventId,
                Guid.NewGuid()
            );
        }
    }

    enum Fei
    {
        None,
        Some,
        All,
    }
}
