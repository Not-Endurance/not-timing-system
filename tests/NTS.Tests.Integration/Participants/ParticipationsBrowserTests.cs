using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The Participations of an Athlete or a Horse in the Ui, in a real browser (#639, ADR-0010): a person who is signed in bookmarks
/// the Athlete they are in their profile and reads that Athlete's Participations as rows, opens one for its Phases and its
/// totals, keeps Horses and opens a Horse in a dialog, and searches for an Athlete or a Horse in either script. A visitor sees
/// none of it. The text is in the language of the browser.
/// </summary>
public sealed class ParticipationsBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    const string CLIPPING = """
        element => ({
            client: element.clientWidth,
            scroll: element.scrollWidth,
            overflow: getComputedStyle(element).overflow,
            textOverflow: getComputedStyle(element).textOverflow,
            whiteSpace: getComputedStyle(element).whiteSpace,
        })
        """;

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ParticipationsBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task An_anonymous_visitor_sees_no_entry_for_the_Participations_and_both_pages_send_them_to_sign_in()
    {
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url("/"));

        await Expect(page.GetByText("Sign In")).ToBeVisibleAsync(Loaded()); // the drawer is a visitor's
        await Expect(page.GetByText("Historic events")).ToBeVisibleAsync(Loaded()); // the entries of the drawer are there
        await Expect(Group(page, "Participations")).ToHaveCountAsync(0);
        await Expect(Entry(page, "My Participations")).ToHaveCountAsync(0);
        await Expect(Entry(page, "Search")).ToHaveCountAsync(0);
        foreach (var path in new[] { "/participations", "/profile" })
        {
            await page.GotoAsync(device.Url(path));
            await page.WaitForURLAsync(
                new Regex(@"/sign-in\?returnUrl=" + Regex.Escape(Uri.EscapeDataString(path))),
                new() { Timeout = PATIENCE }
            );
        }
    }

    [Fact]
    public async Task A_person_picks_their_Athlete_reads_the_Participations_as_rows_and_opens_one()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), $"Мариела {cyrillic}ова", $"Mariela {latin}ova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        var fire = new Participant(Guid.NewGuid(), "Огън", "Fire");
        await RegistrySeed.AthleteAsync(
            mongo,
            tenant,
            ana.Name,
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: ana.NameEnglish,
            id: ana.Id
        );
        var zone = TimeSpan.FromHours(3); // an Event is stored as the instants its days start and end at in its place
        var spring = await HistorySeed.EventAsync(
            mongo,
            tenant,
            "Spring ride",
            "Sofia",
            new DateTimeOffset(2026, 3, 14, 0, 0, 0, zone),
            new DateTimeOffset(2026, 3, 15, 23, 59, 59, zone)
        );
        var autumn = await HistorySeed.EventAsync(
            mongo,
            tenant,
            "Autumn ride",
            "Varna",
            new DateTimeOffset(2025, 11, 2, 0, 0, 0, zone),
            new DateTimeOffset(2025, 11, 2, 23, 59, 59, zone)
        );
        var latest = await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind, rank: 1, competition: "CEI 1*");
        await HistorySeed.CountAlsoAsync(mongo, tenant, spring, "National Championship", latest, rank: 2);
        await HistorySeed.RideAsync(
            mongo,
            tenant,
            autumn,
            1,
            ana,
            fire,
            competition: "CEI 2*",
            eliminated: new Withdrawn()
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        await Expect(Group(page, "Participations")).ToHaveCountAsync(1, Counted());
        await Expect(Entry(page, "My Participations")).ToBeVisibleAsync(Loaded());
        await Expect(Entry(page, "Search")).ToBeVisibleAsync(Loaded());
        await Entry(page, "Search").ClickAsync();
        await page.WaitForURLAsync(device.Url("/participations"), new() { Timeout = PATIENCE });
        await Entry(page, "My Participations").ClickAsync();
        await page.WaitForURLAsync(device.Url("/profile"), new() { Timeout = PATIENCE });
        await page.GetByLabel("My Athlete").FillAsync($"{latin}ova");
        await page.GetByTestId("participant-match").ClickAsync(); // the selector of the Athlete offers Athletes alone

        await Expect(page.GetByTestId("my-athlete")).ToHaveTextAsync($"Mariela {latin}ova", Texted());
        var rows = page.Locator("tr.participation-row");
        await Expect(rows).ToHaveCountAsync(2, Counted());
        await Expect(page.GetByRole(AriaRole.Columnheader, new() { Name = "Horse", Exact = true }))
            .ToBeVisibleAsync(Loaded()); // the other half of the Participation of an Athlete is a Horse
        var newest = rows.Nth(0);
        await Expect(newest.GetByTestId("participation-date")).ToHaveTextAsync("3/14/2026", Texted()); // the day of the place, in the format of the language
        await Expect(newest.GetByTestId("participation-other")).ToHaveTextAsync("Wind", Texted());
        await Expect(newest.GetByTestId("participation-location")).ToHaveTextAsync("Sofia", Texted());
        await Expect(newest.GetByTestId("participation-competition")).ToHaveTextAsync("CEI 1*", Texted());
        await Expect(newest.GetByTestId("participation-placing")).ToHaveTextAsync("1", Texted());
        var oldest = rows.Nth(1);
        await Expect(oldest.GetByTestId("participation-date")).ToHaveTextAsync("11/2/2025", Texted());
        await Expect(oldest.GetByTestId("participation-other")).ToHaveTextAsync("Fire", Texted());
        await Expect(oldest.GetByTestId("participation-placing")).ToHaveTextAsync("Eliminated: WD", Texted());

        await newest.GetByTestId("participation-date").ClickAsync();

        var detail = page.GetByTestId("participation-detail");
        await Expect(detail).ToHaveCountAsync(1, Counted());
        await Expect(detail).ToHaveAttributeAsync("colspan", "6", new() { Timeout = PATIENCE }); // the row that is open spans the six columns of the table
        await Expect(detail.Locator(".participation-table-root")).ToBeVisibleAsync(Loaded());
        await Expect(detail.GetByText("GATE1/40").First).ToBeVisibleAsync(Loaded());
        await Expect(detail.GetByTestId("participation-event")).ToHaveTextAsync("Spring ride", Texted());
        await Expect(detail.GetByTestId("participation-event"))
            .ToHaveAttributeAsync("href", $"/historic-events/{spring}", new() { Timeout = PATIENCE }); // the Event it was in
        await Expect(detail.GetByTestId("participation-also"))
            .ToHaveTextAsync("Also counted in National Championship (Senior): 2", Texted());
        await newest.GetByTestId("participation-date").ClickAsync();
        await Expect(detail).ToHaveCountAsync(0, Counted());
        var toggle = newest.GetByRole(AriaRole.Button); // a person who does not use a pointer opens a row with its button
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false", new() { Timeout = PATIENCE });
        await toggle.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(detail).ToHaveCountAsync(1, Counted());
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true", new() { Timeout = PATIENCE });
        await page.Keyboard.PressAsync("Enter");
        await Expect(detail).ToHaveCountAsync(0, Counted());
        var links = RegistrySeed.Collection(mongo, "user_links");
        Assert.Equal(1, await links.CountDocumentsAsync(new BsonDocument("_id", RegistrySeed.Binary(me.Id))));
    }

    [Fact]
    public async Task The_Horses_of_the_person_are_those_their_Athlete_has_ridden_and_those_they_added_and_a_Horse_opens_in_a_dialog()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), $"Мариела {cyrillic}ова", $"Mariela {latin}ova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        var fire = new Participant(Guid.NewGuid(), "Огън", "Fire");
        var storm = new Participant(Guid.NewGuid(), $"Буря {cyrillic}", $"Storm {latin}");
        var blaze = new Participant(Guid.NewGuid(), "Пламък", "Blaze");
        await RegisterAsync(mongo, tenant, ana);
        foreach (var horse in new[] { wind, fire, storm, blaze })
        {
            await RegistrySeed.HorseAsync(mongo, tenant, horse.Name, horse.NameEnglish, id: horse.Id);
        }

        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        var winter = await HistorySeed.HistoricAsync(mongo, tenant, "Winter ride", "Plovdiv", 40);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind, rank: 3);
        await HistorySeed.RideAsync(mongo, tenant, winter, 1, ana, blaze);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id, fire.Id, blaze.Id); // Fire was added, Wind was ridden, Blaze was both
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");

        var horses = page.GetByTestId("my-horse");
        await Expect(horses).ToHaveTextAsync(new[] { "Wind", "Blaze", "Fire" }, Texted()); // the ones ridden, the latest first, then the ones that were only added
        await Expect(Remove(page, "Fire")).ToBeVisibleAsync(Loaded());
        await Expect(Remove(page, "Blaze")).ToBeVisibleAsync(Loaded()); // a Horse that was added is removed, ridden or not
        await Expect(Remove(page, "Wind")).ToHaveCountAsync(0); // what only follows from the Participations is not removed

        await horses.Filter(new() { HasText = "Wind" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByText("Wind", new() { Exact = true }).First).ToBeVisibleAsync(Loaded());
        var inTheDialog = dialog.Locator("tr.participation-row");
        await Expect(inTheDialog).ToHaveCountAsync(1, Counted());
        await Expect(inTheDialog.GetByTestId("participation-other")).ToHaveTextAsync($"Mariela {latin}ova", Texted()); // the Athlete is the other half
        await Expect(inTheDialog.GetByTestId("participation-placing")).ToHaveTextAsync("3", Texted());
        await Expect(dialog.GetByRole(AriaRole.Columnheader, new() { Name = "Athlete", Exact = true }))
            .ToBeVisibleAsync(Loaded()); // the other half of the Participation of a Horse is an Athlete
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());

        var add = page.GetByLabel("Add a Horse");
        await add.FillAsync($"Storm {latin}");
        await page.GetByTestId("participant-match").ClickAsync(); // the selector of the Horses offers Horses alone
        await Expect(horses).ToHaveTextAsync(new[] { "Wind", "Blaze", "Fire", $"Storm {latin}" }, Texted());
        await Expect(add).ToHaveValueAsync(string.Empty, Filled()); // an input that adds is empty again
        await Remove(page, "Fire").ClickAsync();
        await Expect(horses).ToHaveTextAsync(new[] { "Wind", "Blaze", $"Storm {latin}" }, Texted());
        await Eventually(async () => (await HorsesOfAsync(mongo, me.Id)).SequenceEqual([blaze.Id, storm.Id]));

        await page.ReloadAsync(); // what was added and removed is kept
        await Expect(page.GetByTestId("my-horse"))
            .ToHaveTextAsync(new[] { "Wind", "Blaze", $"Storm {latin}" }, Texted());
    }

    [Fact]
    public async Task A_person_with_no_Athlete_can_still_add_Horses()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var storm = await RegistrySeed.HorseAsync(mongo, tenant, $"Буря {cyrillic}", $"Storm {latin}");
        await RegistrySeed.AthleteAsync(
            mongo,
            tenant,
            $"Буря {cyrillic}ова",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: $"Storm {latin}ova"
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        await Expect(page.GetByLabel("My Athlete")).ToBeVisibleAsync(Loaded());
        var matches = page.GetByTestId("participant-match");

        await page.GetByLabel("My Athlete").FillAsync($"Storm {latin}"); // the name of a Horse and of an Athlete
        await Expect(matches).ToHaveCountAsync(1, Counted()); // the selector of the Athlete offers the Athlete alone
        await Expect(matches).ToContainTextAsync($"Storm {latin}ova", Containing());
        await page.GetByLabel("My Athlete").FillAsync(string.Empty);
        await page.GetByLabel("Add a Horse").FillAsync($"Storm {latin}");
        await Expect(matches).ToHaveCountAsync(1, Counted()); // and the one of the Horses, the Horse alone
        await Expect(matches).ToHaveTextAsync($"Storm {latin}", Texted());
        await matches.ClickAsync();

        await Expect(page.GetByTestId("my-horse")).ToHaveTextAsync(new[] { $"Storm {latin}" }, Texted());
        await Expect(page.GetByLabel("My Athlete")).ToBeVisibleAsync(Loaded()); // there is still no Athlete
        await Eventually(async () => (await HorsesOfAsync(mongo, me.Id)).SequenceEqual([storm]));
    }

    [Fact]
    public async Task The_search_finds_the_Athletes_and_the_Horses_as_two_lists_by_the_words_of_their_names_and_choosing_one_shows_its_Participations()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ivan = new Participant(Guid.NewGuid(), $"Иван {cyrillic}ов", $"Ivan {latin}ov");
        var star = new Participant(Guid.NewGuid(), $"Звезда {cyrillic}ов", $"Star {latin}ov");
        await RegisterAsync(mongo, tenant, ivan);
        await RegistrySeed.HorseAsync(mongo, tenant, star.Name, star.NameEnglish, id: star.Id);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ivan, star, competition: "CEI 1*");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        var input = page.GetByLabel("Name of an Athlete or a Horse");
        var athletes = Matches(page, "Athlete");
        var horses = Matches(page, "Horse");
        var searches = 0;
        page.Request += (_, request) =>
        {
            if (IsSearch(request.Url))
            {
                Interlocked.Increment(ref searches);
            }
        };
        await input.FillAsync(latin[..1]);
        await Task.Delay(1500); // a text with no word of two characters asks for nothing, and what is not asked is not answered either
        await Expect(page.GetByText("Nothing found")).ToHaveCountAsync(0);
        await Expect(athletes).ToHaveCountAsync(0);
        Assert.Equal(0, Volatile.Read(ref searches));

        await input.FillAsync($"{latin}OV"); // in capitals, and the English name of each

        await Expect(athletes).ToContainTextAsync("Athletes", Containing());
        await Expect(horses).ToContainTextAsync("Horses", Containing());
        await Expect(athletes.GetByTestId("participant-match")).ToHaveTextAsync([$"Ivan {latin}ov"], Texted());
        await Expect(horses.GetByTestId("participant-match")).ToHaveTextAsync([$"Star {latin}ov"], Texted());
        await athletes.GetByTestId("participant-match").ClickAsync();
        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync($"Ivan {latin}ov", Containing());
        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync("Athlete", Containing()); // and which kind it is
        var rows = page.Locator("tr.participation-row");
        await Expect(rows).ToHaveCountAsync(1, Counted());
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync($"Star {latin}ov", Texted()); // the Horse, the other half of the Athlete
        await rows.GetByTestId("participation-date").ClickAsync();
        await Expect(page.GetByTestId("participation-detail")).ToHaveCountAsync(1, Counted());

        await horses.GetByTestId("participant-match").ClickAsync();

        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync($"Star {latin}ov", Containing());
        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync("Horse", Containing());
        await Expect(rows).ToHaveCountAsync(1, Counted());
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync($"Ivan {latin}ov", Texted()); // and the Athlete, the other half of the Horse
        await Expect(page.GetByTestId("participation-detail")).ToHaveCountAsync(0, Counted()); // the row that was open for the Athlete is not open for the Horse
        Assert.Equal(2, Volatile.Read(ref searches)); // one for the Athletes and one for the Horses: choosing one asked for no list again

        await input.FillAsync($"{cyrillic}ов"); // the name in the script it is registered in
        await Eventually(async () => Volatile.Read(ref searches) == 4); // the lists are asked for the new text, after the pause
        await Expect(athletes.GetByTestId("participant-match")).ToHaveTextAsync([$"Ivan {latin}ov"], Texted()); // told in the language of the app all the same
        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync($"Star {latin}ov", Containing()); // and the one that was chosen stays

        await page.Locator(".mud-input-clear-button").ClickAsync();

        await Expect(page.GetByTestId("chosen-participant")).ToHaveCountAsync(0, Counted()); // nobody is chosen, and nothing is listed
        await Expect(rows).ToHaveCountAsync(0, Counted());
        await Expect(athletes).ToHaveCountAsync(0, Counted());
        await NoUnhandledErrorAsync(page);
    }

    [Fact]
    public async Task A_list_of_more_than_ten_names_shows_ten_and_the_rest_when_asked_to_and_a_kind_with_none_says_so()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, _) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        for (var i = 0; i < 12; i++)
        {
            await RegistrySeed.HorseAsync(mongo, tenant, $"Horse {latin} {i:00}");
        }

        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        var horses = Matches(page, "Horse");
        var more = horses.GetByRole(AriaRole.Button, new() { Name = "Show more", Exact = true });

        await page.GetByLabel("Name of an Athlete or a Horse").FillAsync($"{latin} horse");

        await Expect(horses.GetByTestId("participant-match")).ToHaveCountAsync(10, Counted());
        await Expect(horses.GetByTestId("participant-match").First).ToHaveTextAsync($"Horse {latin} 00", Texted());
        await Expect(Matches(page, "Athlete").GetByTestId("participant-matches-empty"))
            .ToHaveTextAsync("Nothing found", Texted()); // the Athletes are another list, and have none
        await more.ClickAsync();

        await Expect(horses.GetByTestId("participant-match")).ToHaveCountAsync(12, Counted());
        await Expect(horses.GetByTestId("participant-match").Last).ToHaveTextAsync($"Horse {latin} 11", Texted());
        await Expect(more).ToHaveCountAsync(0, Counted());
        await NoUnhandledErrorAsync(page);
    }

    [Fact]
    public async Task The_search_asks_after_a_pause_and_not_at_each_key()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        await RegisterAsync(mongo, tenant, new Participant(Guid.NewGuid(), $"Иван {cyrillic}ов", $"Ivan {latin}ov"));
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        var searches = 0;
        page.Request += (_, request) =>
        {
            if (IsSearch(request.Url))
            {
                Interlocked.Increment(ref searches);
            }
        };

        await page.GetByLabel("Name of an Athlete or a Horse")
            .PressSequentiallyAsync($"{latin}ov", new() { Delay = 80 });

        await Expect(page.GetByTestId("participant-match")).ToHaveCountAsync(1, Counted());
        Assert.Equal(2, Volatile.Read(ref searches)); // the keys were typed faster than the pause that the search waits for, and each search draws on the budget of the account: one for the Athletes and one for the Horses
    }

    [Fact]
    public async Task A_list_that_cannot_be_read_offers_to_be_read_again_and_does_not_say_there_is_nothing()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        await RegistrySeed.HorseAsync(mongo, tenant, $"Звезда {cyrillic}ов", $"Star {latin}ov");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        var failing = true;
        await page.RouteAsync(
            new Regex(@"/api/athletes/all-tenants\?"),
            async route =>
            {
                if (failing)
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            }
        );

        await page.GetByLabel("Name of an Athlete or a Horse").FillAsync($"{latin}ov");

        var told = page.GetByRole(AriaRole.Dialog);
        await Expect(told).ToContainTextAsync("Unhandled Exception", Containing()); // the person is told
        await told.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        var athletes = Matches(page, "Athlete");
        var reload = athletes.GetByRole(AriaRole.Button, new() { Name = "Reload", Exact = true });
        await Expect(reload).ToBeVisibleAsync(Loaded()); // and is offered to ask again, and not told that there is nothing
        await Expect(athletes.GetByTestId("participant-matches-empty")).ToHaveCountAsync(0);
        await Expect(Matches(page, "Horse").GetByTestId("participant-match"))
            .ToHaveTextAsync([$"Star {latin}ov"], Texted()); // the Horses are another list, which was read
        failing = false;

        await reload.ClickAsync();

        await Expect(athletes.GetByTestId("participant-matches-empty")).ToHaveTextAsync("Nothing found", Texted()); // there are no Athletes of the name, and it is told so
        await Expect(reload).ToHaveCountAsync(0, Counted());
    }

    [Fact]
    public async Task A_list_that_was_asked_for_before_another_was_chosen_shows_nothing_of_what_it_is_told_after()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ivan = new Participant(Guid.NewGuid(), $"Иван {cyrillic}ов");
        var star = new Participant(Guid.NewGuid(), $"Звезда {cyrillic}ов");
        await RegisterAsync(mongo, tenant, ivan);
        await RegistrySeed.HorseAsync(mongo, tenant, star.Name, id: star.Id);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ivan, star);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        await page.RouteAsync(
            new Regex(@"/api/participations/history\?filter=athleteId"),
            async route =>
            {
                await Task.Delay(7000); // the list of the Athlete is told late
                await route.ContinueAsync();
            }
        );
        var input = page.GetByLabel("Name of an Athlete or a Horse");
        var matches = page.GetByTestId("participant-match");
        var rows = page.Locator("tr.participation-row");
        await input.FillAsync($"{cyrillic}ов");
        await Expect(matches).ToHaveCountAsync(2, Counted());
        await matches.Nth(0).ClickAsync(); // the Athlete is chosen, and its list is waited for

        await input.FillAsync($"{cyrillic}ов");
        await Expect(matches).ToHaveCountAsync(2, Counted());
        await matches.Nth(1).ClickAsync(); // and then the Horse, whose list comes at once

        await Expect(rows).ToHaveCountAsync(1, Counted());
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync(ivan.Name, Texted());
        await Task.Delay(9000); // the Athlete's list is told now, to a page that has moved on
        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync(ivan.Name, Texted());
        await Expect(page.GetByTestId("chosen-participant")).ToContainTextAsync(star.Name, Containing());
    }

    [Fact]
    public async Task A_list_that_was_asked_for_twice_shows_the_answer_to_the_second_and_not_the_first_when_it_comes_late()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ivan = new Participant(Guid.NewGuid(), $"Иван {cyrillic}ов");
        var star = new Participant(Guid.NewGuid(), $"Звезда {cyrillic}ов");
        await RegisterAsync(mongo, tenant, ivan);
        await RegistrySeed.HorseAsync(mongo, tenant, star.Name, id: star.Id);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ivan, star);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        var asked = 0;
        await page.RouteAsync(
            new Regex(@"/api/participations/history\?filter=athleteId"),
            async route =>
            {
                if (Interlocked.Increment(ref asked) == 1)
                {
                    await Task.Delay(9000); // the first time the Athlete's list is asked for, it is told late
                }

                await route.ContinueAsync();
            }
        );
        var input = page.GetByLabel("Name of an Athlete or a Horse");
        var matches = page.GetByTestId("participant-match");
        var rows = page.Locator("tr.participation-row");
        await input.FillAsync($"{cyrillic}ов");
        await Expect(matches).ToHaveCountAsync(2, Counted());
        await matches.Nth(0).ClickAsync(); // the Athlete
        await input.FillAsync($"{cyrillic}ов");
        await Expect(matches).ToHaveCountAsync(2, Counted());
        await matches.Nth(1).ClickAsync(); // the Horse
        await Expect(rows).ToHaveCountAsync(1, Counted());
        await input.FillAsync($"{cyrillic}ов");
        await Expect(matches).ToHaveCountAsync(2, Counted());

        await matches.Nth(0).ClickAsync(); // and the Athlete again, whose list is asked for the second time and comes at once

        await Expect(rows).ToHaveCountAsync(1, Counted());
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync(star.Name, Texted());
        await Task.Delay(10000); // the answer to the first time is told now, to a list that is the same one asked for again
        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.GetByTestId("participation-other")).ToHaveTextAsync(star.Name, Texted());
    }

    [Fact]
    public async Task A_Participation_that_cannot_be_read_closes_its_row_and_opening_it_again_asks_again()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        await RegisterAsync(mongo, tenant, ana);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        var rows = page.Locator("tr.participation-row");
        var detail = page.GetByTestId("participation-detail");
        await Expect(rows).ToHaveCountAsync(1, Counted());
        var failing = true;
        await page.RouteAsync(
            new Regex(@"/api/participations/[0-9a-f-]{36}$"),
            async route =>
            {
                if (failing)
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            }
        );

        await rows.GetByTestId("participation-date").ClickAsync();

        var told = page.GetByRole(AriaRole.Dialog);
        await Expect(told).ToContainTextAsync("Unhandled Exception", Containing()); // the person is told
        await told.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(detail).ToHaveCountAsync(0, Counted()); // and the row is closed, not left waiting
        failing = false;
        await rows.GetByTestId("participation-date").ClickAsync();
        await Expect(detail.Locator(".participation-table-root")).ToBeVisibleAsync(Loaded()); // and asked again, it is read
    }

    [Fact]
    public async Task A_list_that_cannot_be_read_says_so_and_is_read_again_when_asked_to()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        await RegisterAsync(mongo, tenant, ana);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        await Expect(page.GetByTestId("my-athlete")).ToBeVisibleAsync(Loaded());
        var failing = true;
        await page.RouteAsync(
            new Regex(@"/api/participations/history\?filter=athleteId[^&]*&page%5Bsize%5D=20&"), // the list, and not the Horses that the Athlete has ridden
            async route =>
            {
                if (failing)
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            }
        );
        await page.ReloadAsync();

        var told = page.GetByRole(AriaRole.Dialog);
        await Expect(told).ToContainTextAsync("Unhandled Exception", Containing()); // the person is told
        await told.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        var reload = page.GetByRole(AriaRole.Button, new() { Name = "Reload", Exact = true });
        await Expect(reload).ToBeVisibleAsync(Loaded()); // and is offered to ask again, and not told that there is nothing
        await Expect(page.GetByTestId("participation-history-empty")).ToHaveCountAsync(0);
        failing = false;
        await reload.ClickAsync();

        await Expect(page.Locator("tr.participation-row")).ToHaveCountAsync(1, Counted());
    }

    [Fact]
    public async Task A_Participation_that_is_gone_when_its_row_is_opened_says_so()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        await RegisterAsync(mongo, tenant, ana);
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        var ride = await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        var rows = page.Locator("tr.participation-row");
        await Expect(rows).ToHaveCountAsync(1, Counted());
        await HistorySeed.DeleteAsync(mongo, ride.Id); // it was removed after the list was read

        await rows.GetByTestId("participation-date").ClickAsync();

        await Expect(page.GetByTestId("participation-detail"))
            .ToContainTextAsync("The Participation was not found.", Containing());
    }

    [Fact]
    public async Task A_list_that_could_not_be_read_and_has_nothing_when_it_is_read_again_says_so()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        await RegisterAsync(mongo, tenant, ana);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        await Expect(page.GetByTestId("my-athlete")).ToBeVisibleAsync(Loaded());
        var failing = true;
        await page.RouteAsync(
            new Regex(@"/api/participations/history\?filter=athleteId[^&]*&page%5Bsize%5D=20&"),
            async route =>
            {
                if (failing)
                {
                    await route.AbortAsync();
                }
                else
                {
                    await route.ContinueAsync();
                }
            }
        );
        await page.ReloadAsync();
        var told = page.GetByRole(AriaRole.Dialog);
        await Expect(told).ToContainTextAsync("Unhandled Exception", Containing());
        await told.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        var reload = page.GetByRole(AriaRole.Button, new() { Name = "Reload", Exact = true });
        await Expect(reload).ToBeVisibleAsync(Loaded());
        failing = false;

        await reload.ClickAsync();

        await Expect(page.GetByTestId("participation-history-empty"))
            .ToHaveTextAsync("No Participations yet.", Texted());
        await Expect(reload).ToHaveCountAsync(0, Counted()); // it was read, and it is empty: there is nothing to ask for again
    }

    [Fact]
    public async Task An_Athlete_with_no_Participations_says_so()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        await RegisterAsync(mongo, tenant, ana);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");

        await Expect(page.GetByTestId("participation-history-empty"))
            .ToHaveTextAsync("No Participations yet.", Texted());
        await Expect(page.Locator("tr.participation-row")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_search_that_is_not_wanted_any_more_because_the_person_typed_on_is_not_a_failure()
    {
        var mongo = _host.MongoConnectionString;
        var (_, first) = HistorySeed.NameMarker();
        var (_, second) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        await RegisterAsync(mongo, tenant, new Participant(Guid.NewGuid(), $"Иван {first}"));
        await RegisterAsync(mongo, tenant, new Participant(Guid.NewGuid(), $"Мария {second}"));
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/participations");
        await page.RouteAsync(
            new Regex(@"/api/(athletes|horses)/all-tenants\?"),
            async route =>
            {
                await Task.Delay(2500); // the Api is slow to answer
                try
                {
                    await route.ContinueAsync();
                }
                catch (PlaywrightException)
                {
                    // the page has abandoned the request meanwhile
                }
            }
        );
        var input = page.GetByLabel("Name of an Athlete or a Horse");
        var matches = page.GetByTestId("participant-match");

        await input.FillAsync(first);
        await Task.Delay(1500); // the search for it was sent and is waited for
        await input.FillAsync(second); // and what was asked for is not wanted now: it finds another Athlete

        await Expect(matches).ToHaveTextAsync([$"Мария {second}"], Texted());
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0); // nobody is told of an error
        await Task.Delay(4000); // the answers to the text that is not wanted are told now, to a list that has moved on
        await Expect(matches).ToHaveTextAsync([$"Мария {second}"], Texted());
        await NoUnhandledErrorAsync(page);
    }

    [Fact]
    public async Task A_picker_asks_after_a_pause_and_a_search_that_is_not_wanted_any_more_is_not_a_failure()
    {
        var mongo = _host.MongoConnectionString;
        var (_, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        await RegistrySeed.HorseAsync(mongo, tenant, $"Буря {cyrillic}");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        var searches = 0;
        await page.RouteAsync(
            new Regex(@"/api/horses/all-tenants\?"),
            async route =>
            {
                Interlocked.Increment(ref searches);
                await Task.Delay(4000); // the Api is slow to answer
                try
                {
                    await route.ContinueAsync();
                }
                catch (PlaywrightException)
                {
                    // the page has abandoned the request meanwhile
                }
            }
        );
        var add = page.GetByLabel("Add a Horse");

        await add.PressSequentiallyAsync(cyrillic[..5], new() { Delay = 300 }); // the keys come slower than the pause of the autocomplete, and faster than the pause of this search
        await Task.Delay(1500); // and the search for them was sent and is waited for
        Assert.Equal(1, Volatile.Read(ref searches));
        await add.PressSequentiallyAsync(cyrillic[5..], new() { Delay = 300 }); // and what was asked for is not wanted now

        await Expect(page.GetByTestId("participant-match")).ToHaveCountAsync(1, Counted());
        Assert.Equal(2, Volatile.Read(ref searches));
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0); // nobody is told of an error
        await NoUnhandledErrorAsync(page);
    }

    [Fact]
    public async Task The_Competition_is_clipped_to_a_fixed_width_in_the_row_and_complete_on_hover()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        await RegisterAsync(mongo, tenant, ana);
        const string LONG =
            "CEI 3* 160 km International Endurance Championship of the Republic of Bulgaria for Young Riders";
        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        var autumn = await HistorySeed.HistoricAsync(mongo, tenant, "Autumn ride", "Varna", 100);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind, competition: LONG);
        await HistorySeed.RideAsync(mongo, tenant, autumn, 1, ana, wind, competition: "CEI 1*");
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");

        var cells = page.GetByTestId("participation-competition");
        await Expect(cells).ToHaveCountAsync(2, Counted());
        var clipped = await cells.Nth(0).EvaluateAsync<JsonElement>(CLIPPING);
        var whole = await cells.Nth(1).EvaluateAsync<JsonElement>(CLIPPING);
        Assert.Equal(LONG, await cells.Nth(0).GetAttributeAsync("title")); // the whole name is what the browser shows on hover
        Assert.Equal("ellipsis", clipped.GetProperty("textOverflow").GetString());
        Assert.Equal("hidden", clipped.GetProperty("overflow").GetString());
        Assert.Equal("nowrap", clipped.GetProperty("whiteSpace").GetString());
        Assert.True(clipped.GetProperty("scroll").GetInt32() > clipped.GetProperty("client").GetInt32()); // it does not fit and is cut
        Assert.Equal(clipped.GetProperty("client").GetInt32(), whole.GetProperty("client").GetInt32()); // the cell is as wide whatever the name
        Assert.Equal("CEI 1*", await cells.Nth(1).GetAttributeAsync("title"));
    }

    [Fact]
    public async Task A_list_of_more_than_a_page_shows_a_page_and_the_rest_when_asked_for_more()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), "Мариела Тестова", "Mariela Testova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        await RegisterAsync(mongo, tenant, ana);
        var recent = await HistorySeed.HistoricAsync(mongo, tenant, "Long ride", "Sofia", 10);
        var old = await HistorySeed.HistoricAsync(mongo, tenant, "Old ride", "Varna", 60);
        for (var number = 1; number <= 20; number++)
        {
            await HistorySeed.RideAsync(mongo, tenant, recent, number, ana, wind, competition: $"CEI {number}*");
        }

        await HistorySeed.RideAsync(mongo, tenant, old, 1, ana, wind);

        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");

        var rows = page.Locator("tr.participation-row");
        var more = page.GetByRole(AriaRole.Button, new() { Name = "Show more", Exact = true });
        await Expect(rows).ToHaveCountAsync(20, Counted()); // a page
        await Expect(more).ToBeVisibleAsync(Loaded());
        await more.ClickAsync();
        await Expect(rows).ToHaveCountAsync(21, Counted()); // and the rest, which is older
        await Expect(more).ToHaveCountAsync(0, Counted());
        await Expect(rows.Nth(0).GetByTestId("participation-location")).ToHaveTextAsync("Sofia", Texted());
        await Expect(rows.Nth(20).GetByTestId("participation-location")).ToHaveTextAsync("Varna", Texted());
    }

    [Fact]
    public async Task A_person_takes_the_bookmark_of_their_Athlete_away_and_the_Horses_that_followed_from_it_go_with_it()
    {
        var mongo = _host.MongoConnectionString;
        var (latin, cyrillic) = HistorySeed.NameMarker();
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        var ana = new Participant(Guid.NewGuid(), $"Мариела {cyrillic}ова", $"Mariela {latin}ova");
        var wind = new Participant(Guid.NewGuid(), "Вятър", "Wind");
        var fire = new Participant(Guid.NewGuid(), "Огън", "Fire");
        await RegisterAsync(mongo, tenant, ana);
        foreach (var horse in new[] { wind, fire })
        {
            await RegistrySeed.HorseAsync(mongo, tenant, horse.Name, horse.NameEnglish, id: horse.Id);
        }

        var spring = await HistorySeed.HistoricAsync(mongo, tenant, "Spring ride", "Sofia", 10);
        await HistorySeed.RideAsync(mongo, tenant, spring, 1, ana, wind);
        await HistorySeed.LinkAsync(mongo, me.Id, ana.Id, fire.Id);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, me.Email, "/profile");
        var horses = page.GetByTestId("my-horse");
        await Expect(horses).ToHaveTextAsync(new[] { "Wind", "Fire" }, Texted());
        await Expect(page.Locator("tr.participation-row")).ToHaveCountAsync(1, Counted());

        await Remove(page, $"Mariela {latin}ova").ClickAsync();

        await Expect(page.GetByTestId("my-athlete")).ToHaveCountAsync(0, Counted());
        await Expect(page.GetByLabel("My Athlete")).ToBeVisibleAsync(Loaded()); // the selector is back
        await Expect(page.Locator("tr.participation-row")).ToHaveCountAsync(0, Counted());
        await Expect(horses).ToHaveTextAsync(new[] { "Fire" }, Texted()); // what was ridden went with the Athlete, what was added stays
        await page.ReloadAsync();
        await Expect(page.GetByLabel("My Athlete")).ToBeVisibleAsync(Loaded()); // and it is kept
        await Expect(page.GetByTestId("my-horse")).ToHaveTextAsync(new[] { "Fire" }, Texted());
    }

    [Theory]
    [InlineData("bg-BG", "Участия", "Моите участия", "Търси", "Участия")]
    public async Task The_text_is_in_the_language_of_the_browser(
        string locale,
        string group,
        string mine,
        string search,
        string section
    )
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var me = await TenancySeed.AccountAsync(mongo, tenant, name: "Vera Georgieva");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress, locale);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await SignInAndOpenAsync(_host, device, me.Email, "/profile");

        await Expect(Group(page, group)).ToHaveCountAsync(1, Counted());
        await Expect(Entry(page, mine)).ToBeVisibleAsync(Loaded());
        await Expect(Entry(page, search)).ToBeVisibleAsync(Loaded());
        await Expect(page.GetByTestId("profile-participations").GetByRole(AriaRole.Heading, new() { Name = section }))
            .ToBeVisibleAsync(Loaded());
    }

    /// <summary>
    /// The app has met no error it could not handle. When a render fails, Blazor shows its error bar and leaves the page as it
    /// was, so a list that stopped following what it is told looks right until this is asked.
    /// </summary>
    static Task NoUnhandledErrorAsync(IPage page)
    {
        return Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
    }

    /// <summary>The list of the Athletes, or of the Horses, that the search found (kind is Athlete or Horse).</summary>
    static ILocator Matches(IPage page, string kind)
    {
        return page.Locator($"[data-testid=participant-matches][data-kind={kind}]");
    }

    /// <summary>Whether a request is the search of the registry of Athletes or of the registry of Horses.</summary>
    static bool IsSearch(string url)
    {
        return url.Contains("/api/athletes/all-tenants?", StringComparison.Ordinal)
            || url.Contains("/api/horses/all-tenants?", StringComparison.Ordinal);
    }

    /// <summary>The button that takes a Horse away from the Horses of the person, by the Horse.</summary>
    static ILocator Remove(IPage page, string horse)
    {
        return page.GetByRole(AriaRole.Button, new() { Name = $"Remove {horse}", Exact = true });
    }

    /// <summary>An Athlete as a row of the registry of the Tenant, with the id of the Participant.</summary>
    static async Task RegisterAsync(string mongo, string tenant, Participant athlete)
    {
        await RegistrySeed.AthleteAsync(
            mongo,
            tenant,
            athlete.Name,
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            nameEnglish: athlete.NameEnglish,
            id: athlete.Id
        );
    }

    /// <summary>The Horses the account has added, in the order it added them.</summary>
    static async Task<List<Guid>> HorsesOfAsync(string mongo, Guid account)
    {
        var links = await RegistrySeed
            .Collection(mongo, "user_links")
            .Find(new BsonDocument("_id", RegistrySeed.Binary(account)))
            .FirstOrDefaultAsync();
        return [.. links?["HorseIds"].AsBsonArray.Select(x => x.AsGuid) ?? []];
    }

    /// <summary>A group of the drawer, by its title.</summary>
    static ILocator Group(IPage page, string title)
    {
        return page.Locator($"nav.mud-nav-group[aria-label='{title}']");
    }

    /// <summary>An entry of the drawer, by its text: it is no link and no button for the browser to name, only a click.</summary>
    static ILocator Entry(IPage page, string text)
    {
        return page.Locator(".mud-nav-link-text").GetByText(text, new() { Exact = true });
    }

    static LocatorAssertionsToHaveTextOptions Texted()
    {
        return new LocatorAssertionsToHaveTextOptions { Timeout = PATIENCE };
    }

    static LocatorAssertionsToContainTextOptions Containing()
    {
        return new LocatorAssertionsToContainTextOptions { Timeout = PATIENCE };
    }
}
