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

    [Fact]
    public async Task The_Main_Operator_sets_up_its_Event_from_the_Console_and_a_page_that_is_reloaded_goes_back_to_the_Setup()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        var id = await EventSeed.SetupAsync(mongo, tenant, mainOperator.Id, "The Cup");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);

        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, mainOperator.Email);
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
        await dialog.GetByLabel("Email address").FillAsync("nobody.yet@example.test");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Link a person", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Invitation pending")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task The_Main_Operator_registers_a_Horse_and_an_Athlete_and_a_Combination_of_the_Setup_picks_them()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await EventSeed.SetupAsync(mongo, tenant, mainOperator.Id, "The Cup");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        var dialog = page.GetByRole(AriaRole.Dialog);

        await page.GotoAsync(device.Url("/console"));
        await page.WaitForURLAsync(new Regex(@"/sign-in\?returnUrl="), new() { Timeout = PATIENCE });
        await SignInOnTheHostPagesAsync(_host, page, mainOperator.Email);
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

    /// <summary>Presses the button that makes a row in a list of the Setup: an icon beside the heading of the list.</summary>
    static async Task AddToListAsync(IPage page, string list)
    {
        await page.Locator(
                $"xpath=//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][normalize-space()='{list}']/following-sibling::button[last()]"
            )
            .ClickAsync();
    }
}
