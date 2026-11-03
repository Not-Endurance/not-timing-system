using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The registry of Athletes and Horses in the Console, in a real browser (#646, ADR-0010): a form that is about to register an
/// Athlete who looks like one that is registered says so, and names the row, and does not stop the person who means it.
/// </summary>
public sealed class ConsoleRegistryBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleRegistryBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_form_that_is_about_to_register_an_Athlete_that_looks_like_one_that_is_registered_says_so_and_still_registers_it()
    {
        var mongo = _host.MongoConnectionString;
        var token = Guid.NewGuid().ToString("N")[..8];
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var other = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var consoleOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await EventSeed.SetupAsync(mongo, tenant, consoleOperator.Id, "The Cup");
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        await RegistrySeed.AthleteAsync(mongo, tenant, $"Ivan Petrov {token}", bulgaria, null, "777");
        await RegistrySeed.AthleteAsync(mongo, other, $"Maria Elsewhere {token}", bulgaria, null, "888");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, consoleOperator.Email, "/console/registry/athletes-horses");
        await AddToListAsync(page, "Athletes");
        var dialog = page.GetByRole(AriaRole.Dialog);
        var alert = dialog.GetByTestId("console-same-rows");

        // a name that is there, in another case and with more spaces than it was written with
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync($"  ivan   PETROV {token}");
        await dialog.GetByLabel("Name", new() { Exact = true }).PressAsync("Tab");
        await Expect(alert).ToContainTextAsync($"Ivan Petrov {token}", Visible());
        await Expect(alert).ToContainTextAsync("the same name");

        // one of another Tenant, by its name in English
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync("Нов");
        await dialog.GetByLabel("Name in English").FillAsync($"maria elsewhere {token}");
        await dialog.GetByLabel("Name in English").PressAsync("Tab");
        await Expect(alert).ToContainTextAsync($"Maria Elsewhere {token}", Visible());
        await Expect(alert).ToContainTextAsync("of another federation");

        // a FEI ID that is there, as the number it is
        await dialog.GetByLabel("Name in English").FillAsync(string.Empty);
        await dialog.GetByLabel("Name in English").PressAsync("Tab");
        await dialog.GetByLabel("FEI ID").FillAsync("0777");
        await dialog.GetByLabel("Name", new() { Exact = true }).ClickAsync(); // leaves the field without opening the country that follows it
        await Expect(alert).ToContainTextAsync("the same FEI ID", Visible());

        // and it is a warning: the person who means it registers the Athlete all the same
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create", Exact = true }).ClickAsync();
        await Eventually(
            async () => (await RegistrySeed.Collection(mongo, "athletes").CountDocumentsAsync(Named("Нов"))) == 1
        );
    }

    static LocatorAssertionsToContainTextOptions Visible()
    {
        return new LocatorAssertionsToContainTextOptions { Timeout = PATIENCE };
    }

    static BsonDocument Named(string name)
    {
        return new BsonDocument("Name", name);
    }

    /// <summary>Presses the button that makes a row in a list: an icon beside the heading of the list.</summary>
    static async Task AddToListAsync(IPage page, string list)
    {
        await page.Locator(
                $"xpath=//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6][normalize-space()='{list}']/following-sibling::button[last()]"
            )
            .ClickAsync();
    }
}
