using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Harness.Drivers;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.UiBrowser;

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
    public async Task A_Tenant_Root_makes_an_Event_and_assigns_a_Console_Operator_who_then_runs_it_from_the_Console_beside_it()
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
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true })).ToBeVisibleAsync(); // it runs it, as Root
        await row.GetByRole(AriaRole.Button, new() { Name = "Console Operators" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByTestId("console-operators-empty")).ToBeVisibleAsync(Loaded()); // nobody is assigned when an Event is made
        await dialog.GetByLabel("Find an account by name").FillAsync("Boris Iv");
        await page.Locator(".mud-popover-open .mud-list-item")
            .Filter(new() { HasText = "Boris Ivanov" })
            .First.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Assign", Exact = true }).ClickAsync();
        await Expect(dialog.GetByTestId("console-operators-list").GetByText("Boris Ivanov")).ToBeVisibleAsync(Loaded());
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true })).ToBeVisibleAsync(); // and having assigned one, it still does
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Results", Exact = true }))
            .ToHaveCountAsync(0, Counted()); // there are no Results of an Event that has not started
        await device.FetchAsync("DELETE", "/api/sessions/current");
        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, next.Email);
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        var theirs = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        await Expect(theirs.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true }))
            .ToBeVisibleAsync(Loaded());
        await Expect(theirs.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true })).ToBeVisibleAsync();
        await Expect(theirs.GetByRole(AriaRole.Button, new() { Name = "Console Operators" }))
            .ToHaveCountAsync(0, Counted()); // only Root assigns them
    }

    [Fact]
    public async Task A_Live_Event_is_reset_when_the_Console_Operator_types_the_code_and_confirms_and_not_when_it_does_not()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var id = await EventSeed.SetupAsync(mongo, tenant, consoleOperator.Id, "The Cup");
        await EventSeed.StartAsync(mongo, id, tenant, consoleOperator.Id, DateTimeOffset.UtcNow.AddDays(1));
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, "/console");
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        var dashboard = row.GetByRole(AriaRole.Button, new() { Name = "Dashboard", Exact = true });
        var dialog = page.GetByRole(AriaRole.Dialog);
        var confirm = dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true });
        await Expect(dashboard).ToBeVisibleAsync(Loaded());

        // not until the code is typed, and not when the person gives it up
        await row.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).ClickAsync();
        await Expect(confirm).ToBeDisabledAsync(new() { Timeout = PATIENCE });
        await dialog.GetByLabel("Confirmation code").FillAsync("1336");
        await Expect(confirm).ToBeDisabledAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Expect(dashboard).ToBeVisibleAsync(); // the Event is as it was
        await Eventually(async () => await RegistrySeed.StoredAsync(mongo, "event_informations", id) != null);

        // and with the code, when the person confirms, it is reset: the Setup is what it has again
        await row.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).ClickAsync();
        await dialog.GetByLabel("Confirmation code").FillAsync("1337");
        await confirm.ClickAsync();
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true })).ToBeVisibleAsync(Loaded());
        await Expect(dashboard).ToHaveCountAsync(0, Counted());
        await Eventually(async () => await RegistrySeed.StoredAsync(mongo, "event_informations", id) == null);
    }

    [Fact]
    public async Task A_start_number_that_two_Competitions_hold_is_taken_out_of_the_one_the_person_chooses_and_the_Event_starts()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var id = await EventSeed.FullSetupAsync(
            mongo,
            tenant,
            consoleOperator.Id,
            "Same Number Cup",
            startNumberInTwoCompetitions: true
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, "/console");
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Same Number Cup" });

        await row.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true }).ClickAsync();

        // the Setup is not whole, and the person is asked which Competition the Participation is taken out of
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByTestId("console-start-issue")).ToHaveCountAsync(1, Counted());
        var start = dialog.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true });
        await Expect(start).ToBeDisabledAsync(new() { Timeout = PATIENCE }); // nothing is chosen yet
        await dialog.GetByText("Remove from CEI 2*").ClickAsync();
        await Expect(start).ToBeEnabledAsync();
        await start.ClickAsync();

        // it is taken out of the one that was chosen and not out of the other, and the Event starts: the person is taken to its dashboard
        await page.WaitForURLAsync(device.Url($"/console/events/{id}/dashboard"), new() { Timeout = PATIENCE });
        var setup = await EventSeed.Setups(mongo).Find(new BsonDocument("_id", EventSeed.Binary(id))).FirstAsync();
        var held = setup["Competitions"]
            .AsBsonArray.ToDictionary(x => x["Name"].AsString, x => x["Participations"].AsBsonArray.Count);
        Assert.Equal(2, held["CEI 1*"]); // the one it holds and the one of its own
        Assert.Equal(1, held["CEI 2*"]); // the one of its own: the number is taken out of it
    }

    [Fact]
    public async Task A_correction_that_leaves_the_Setup_not_whole_says_what_is_left_and_the_Event_does_not_start()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var id = await EventSeed.FullSetupAsync(
            mongo,
            tenant,
            consoleOperator.Id,
            "Empty Competition Cup",
            startNumberInTwoCompetitions: true,
            eachHasAnother: false
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, "/console");
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Empty Competition Cup" });
        await row.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog.GetByTestId("console-start-issue")).ToHaveCountAsync(1, Counted());

        await dialog.GetByText("Remove from CEI 2*").ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true }).ClickAsync();

        // the number is taken out of the Competition that was chosen, which is left with none, and the dialog says that and stays
        await Expect(dialog.GetByText("must have at least one participation")).ToBeVisibleAsync(Loaded());
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "start", Exact = true }))
            .ToHaveCountAsync(0, Counted()); // there is nothing left that the person can settle here
        Assert.Equal(device.Url("/console"), page.Url);
        var setup = await EventSeed.Setups(mongo).Find(new BsonDocument("_id", EventSeed.Binary(id))).FirstAsync();
        var held = setup["Competitions"]
            .AsBsonArray.ToDictionary(x => x["Name"].AsString, x => x["Participations"].AsBsonArray.Count);
        Assert.Equal(1, held["CEI 1*"]);
        Assert.Equal(0, held["CEI 2*"]);
        Assert.Equal(
            0,
            await EventSeed.Cores(mongo).CountDocumentsAsync(new BsonDocument("_id", EventSeed.Binary(id)))
        ); // and no Event was made
    }

    [Fact]
    public async Task The_Console_Operator_sets_up_its_Event_from_the_Console_and_a_page_that_is_reloaded_goes_back_to_the_Setup()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var id = await EventSeed.SetupAsync(mongo, tenant, consoleOperator.Id, "The Cup");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, consoleOperator.Email);
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        await row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true }).ClickAsync();
        var name = page.GetByLabel("Name", new() { Exact = true });
        await Expect(name).ToHaveValueAsync("The Cup", Filled());

        // the form of the Event is stored by the Api and read from it again when the page is opened again
        await name.FillAsync("The Big Cup");
        await page.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Updated", new() { Exact = true }).First).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("The Big Cup", Filled());

        // a Loop and a Competition are made in the lists of the Setup, each in a dialog
        var dialog = page.GetByRole(AriaRole.Dialog);
        await AddToListAsync(page, "Loops");
        await dialog.GetByLabel("Loop distance (km)").FillAsync("40");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("40km", new() { Exact = true })).ToBeVisibleAsync();
        await AddToListAsync(page, "Competitions");
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Ride one");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Ride one (0)")).ToBeVisibleAsync();

        // the page of the Competition is handed its form by the list; opened by its address it has none, and it is the Setup
        // that the person is taken to
        await page.Locator("xpath=//p[contains(normalize-space(), 'Ride one')]/following-sibling::button[1]")
            .ClickAsync();
        await Expect(page.GetByText("Configure Competition").First).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByLabel("Name", new() { Exact = true })).ToHaveValueAsync("The Big Cup", Filled());

        // an Operator is linked by an address that has no account yet, who waits as an invitation
        await page.GetByRole(AriaRole.Button, new() { Name = "Link a person", Exact = true }).ClickAsync();
        var link = dialog.GetByRole(AriaRole.Button, new() { Name = "Link a person", Exact = true });
        await Expect(link).ToBeDisabledAsync(new() { Timeout = PATIENCE }); // there is no address to link yet
        await dialog.GetByLabel("Email address").FillAsync("nobody.yet@example.test");
        await Expect(link).ToBeEnabledAsync();
        await link.ClickAsync();
        await Expect(page.GetByText("Invitation pending")).ToBeVisibleAsync();

        // and the person is taken out again by the row that names them
        await page.GetByTestId("console-grants")
            .GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })
            .ClickAsync();
        await Expect(page.GetByText("Invitation pending")).ToHaveCountAsync(0, Counted());
        Assert.Equal(
            0,
            await EventSeed
                .Grants(mongo)
                .CountDocumentsAsync(
                    new BsonDocument
                    {
                        { "EventId", EventSeed.Binary(id) },
                        { "Kind", new BsonDocument("$ne", "ConsoleOperator") }, // the Console Operator is not one of the people linked
                    }
                )
        );
    }

    [Fact]
    public async Task The_Console_Operator_registers_a_Horse_and_an_Athlete_and_a_Combination_of_the_Setup_picks_them()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await EventSeed.SetupAsync(mongo, tenant, consoleOperator.Id, "The Cup");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var dialog = page.GetByRole(AriaRole.Dialog);

        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, consoleOperator.Email);
        await page.WaitForURLAsync(device.Url("/console"), new() { Timeout = PATIENCE });
        await page.GetByRole(AriaRole.Button, new() { Name = "Athletes and Horses", Exact = true }).ClickAsync();

        // the registry of the Tenant: a Horse and an Athlete that rides for the country of the Tenant
        await AddToListAsync(page, "Horses");
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Barza");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Barza", new() { Exact = true })).ToBeVisibleAsync(Loaded());
        await AddToListAsync(page, "Athletes");
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Ivan Petrov");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Ivan Petrov").First).ToBeVisibleAsync(Loaded());

        // a Combination of the Setup picks them from it by what the person types
        await page.GotoAsync(device.Url("/console"));
        var row = page.GetByRole(AriaRole.Row).Filter(new() { HasText = "The Cup" });
        await row.GetByRole(AriaRole.Button, new() { Name = "Setup", Exact = true }).ClickAsync();
        await AddToListAsync(page, "Combinations");
        await dialog.GetByLabel("Combination Number").FillAsync("7");
        await dialog.GetByLabel("Athlete").FillAsync("Ivan");
        await page.GetByText("Ivan Petrov").First.ClickAsync();
        await dialog.GetByLabel("Horse").FillAsync("Barz");
        await page.GetByText("Barza").First.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Expect(page.GetByText("#7", new() { Exact = false }).First).ToBeVisibleAsync(Loaded());
    }

    [Fact]
    public async Task The_Console_Operator_runs_a_Live_Event_from_the_dashboard_recording_a_time_and_eliminating_a_Participation()
    {
        var mongo = _host.MongoConnectionString;
        var scene = await LiveScene.CreateAsync(mongo);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(
            _host,
            device,
            scene.ConsoleOperator.Email,
            $"/console/events/{scene.EventId}/dashboard"
        );

        // the Participations of the Event, one of which is selected
        await Expect(ChipOf(page, 2)).ToBeVisibleAsync(Loaded());
        await ChipOf(page, 2).ClickAsync();

        // a time the Console Operator names is recorded by the server and shows in the table of the Participation
        var time = page.GetByTestId("console-time-panel").GetByLabel("Time");
        await page.GetByTestId("console-time-panel")
            .GetByRole(AriaRole.Button, new() { Name = "Arrive", Exact = true })
            .ClickAsync(); // with no time typed there is nothing to record, and the person is told so
        await Expect(page.GetByText("Time format is incorect")).ToBeVisibleAsync();
        await Expect(page.GetByText("Time format is incorect")).ToHaveCountAsync(0, Counted()); // until it is gone
        await time.ClickAsync();
        await time.PressAsync("Control+A"); // over the 00:00:00 that the field starts with
        await time.PressSequentiallyAsync("999999"); // which is no time of the day
        await page.GetByTestId("console-time-panel")
            .GetByRole(AriaRole.Button, new() { Name = "Arrive", Exact = true })
            .ClickAsync();
        await Expect(page.GetByText("Time format is incorect")).ToBeVisibleAsync();
        await Expect(page.GetByText("Time format is incorect")).ToHaveCountAsync(0, Counted());
        await time.ClickAsync();
        await time.PressAsync("Control+A");
        await time.PressSequentiallyAsync("070000");
        await page.GetByTestId("console-time-panel")
            .GetByRole(AriaRole.Button, new() { Name = "Arrive", Exact = true })
            .ClickAsync();
        await Expect(page.GetByText("The time was recorded.")).ToBeVisibleAsync();
        await Expect(page.GetByText("07:00:00", new() { Exact = true }).First).ToBeVisibleAsync();

        // an elimination is made and restored, each stored
        var panel = page.GetByTestId("console-eliminations-panel");
        await panel.GetByText("WD", new() { Exact = true }).ClickAsync();
        await panel.GetByRole(AriaRole.Button, new() { Name = "Eliminate", Exact = true }).ClickAsync();
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Restore", Exact = true })).ToBeEnabledAsync();
        await Eventually(
            async () => (await scene.StoredAsync(2)).TryGetValue("Eliminated", out var kept) && !kept.IsBsonNull
        );
        await panel.GetByRole(AriaRole.Button, new() { Name = "Restore", Exact = true }).ClickAsync();
        await Eventually(
            async () => !(await scene.StoredAsync(2)).TryGetValue("Eliminated", out var kept) || kept.IsBsonNull
        );
        await Expect(panel.GetByRole(AriaRole.Button, new() { Name = "Restore", Exact = true }))
            .ToHaveCountAsync(0, Counted());
    }

    [Fact]
    public async Task The_dashboard_works_in_two_tabs_and_an_edit_from_a_tab_that_is_stale_is_refused_and_keeps_what_was_typed()
    {
        var mongo = _host.MongoConnectionString;
        var scene = await LiveScene.CreateAsync(mongo);
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var path = $"/console/events/{scene.EventId}/dashboard";
        var first = device.Page;
        first.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, scene.ConsoleOperator.Email, path);
        var second = await device.Context.NewPageAsync(); // the same person, in another tab
        second.SetDefaultTimeout(PATIENCE);
        await second.GotoAsync(device.Url(path));
        await Expect(ChipOf(first, 1)).ToBeVisibleAsync(Loaded());
        await Expect(ChipOf(second, 1)).ToBeVisibleAsync(Loaded());

        // what the first tab does reaches the second one as it happens
        var elimination = first.GetByTestId("console-eliminations-panel");
        await elimination.GetByText("WD", new() { Exact = true }).ClickAsync();
        await elimination.GetByRole(AriaRole.Button, new() { Name = "Eliminate", Exact = true }).ClickAsync();
        await Expect(
                second
                    .GetByTestId("console-eliminations-panel")
                    .GetByRole(AriaRole.Button, new() { Name = "Restore", Exact = true })
            )
            .ToBeEnabledAsync(new() { Timeout = PATIENCE });

        // and a write whose news did not reach it (a connection that was down) leaves it with a Participation that is stale
        await scene.WriteBehindTheBackOfTheTabsAsync(1);

        // the second tab edits the times of its Participation, which has changed since it saw it: the Api refuses, the form
        // stays as it was typed and is sent again over what is there now
        await second.GetByText("GATE1/40").First.ClickAsync();
        var dialog = second.GetByRole(AriaRole.Dialog);
        var arrive = dialog.GetByLabel("Arrive", new() { Exact = true });
        await arrive.ClickAsync();
        await arrive.PressAsync("Control+A");
        await arrive.PressSequentiallyAsync("083000");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
        await Expect(second.GetByText("was changed meanwhile")).ToBeVisibleAsync();
        await Expect(arrive).ToHaveValueAsync("08:30:00", Filled());
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0, Counted());
        await Expect(second.GetByText("08:30:00", new() { Exact = true }).First).ToBeVisibleAsync();
        var stored = await scene.StoredAsync(1);
        Assert.False(stored["Eliminated"].IsBsonNull); // what the first tab did is still there
        Assert.Contains("Manual", stored["Phases"][0]["Events"].ToString(), StringComparison.Ordinal);
    }

    /// <summary>Presses the button that makes a row in a list of the Setup: an icon beside the heading of the list.</summary>
    static async Task AddToListAsync(IPage page, string list)
    {
        await page.Locator(
                $"xpath=//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][normalize-space()='{list}']/following-sibling::button[last()]"
            )
            .ClickAsync();
    }

    /// <summary>A Live Event with two Participations that started at six in the morning, and the Console Operator who runs it.</summary>
    sealed class LiveScene
    {
        public static async Task<LiveScene> CreateAsync(string mongo)
        {
            var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
            var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
            var eventId = await EventSeed.LiveAsync(mongo, tenant, consoleOperator.Id, DateTimeOffset.UtcNow);
            var ids = new List<Guid>();
            foreach (var number in new[] { 1, 2 })
            {
                var participation = IntegrationPayloadFactory.ActiveParticipation(
                    eventId,
                    number,
                    Guid.NewGuid(),
                    startTime: new DateTimeOffset(DateTime.Today.AddHours(6))
                );
                await EventSeed.ParticipationAsync(mongo, tenant, participation);
                ids.Add(participation.Id);
            }

            return new LiveScene(mongo, consoleOperator, eventId, ids);
        }

        readonly string _mongo;
        readonly IReadOnlyList<Guid> _participations;

        LiveScene(string mongo, TenancySeed.Seeded consoleOperator, Guid eventId, IReadOnlyList<Guid> participations)
        {
            _mongo = mongo;
            _participations = participations;
            ConsoleOperator = consoleOperator;
            EventId = eventId;
        }

        public TenancySeed.Seeded ConsoleOperator { get; }
        public Guid EventId { get; }

        public async Task<MongoDB.Bson.BsonDocument> StoredAsync(int number)
        {
            return (await RegistrySeed.StoredAsync(_mongo, "event_participations", _participations[number - 1]))!;
        }

        /// <summary>A write that no tab is told of: the version of the Participation moves on and nothing is announced.</summary>
        public async Task WriteBehindTheBackOfTheTabsAsync(int number)
        {
            await RegistrySeed
                .Collection(_mongo, "event_participations")
                .UpdateOneAsync(
                    new MongoDB.Bson.BsonDocument("_id", RegistrySeed.Binary(_participations[number - 1])),
                    new MongoDB.Bson.BsonDocument("$inc", new MongoDB.Bson.BsonDocument("Version", 1))
                );
        }
    }
}
