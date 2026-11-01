using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Drivers;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.UiBrowser;

namespace NTS.Tests.Integration;

/// <summary>
/// The guards of the registry of Athletes and Horses in the Console, in a real browser (#635, ADR-0010): a form that is about to
/// register an Athlete or a Horse whose name is a registered one in the other script, or a spelling of it, says so and names
/// the row; and a row that a Participation refers to is not deleted, the person is told why and the row stays in the list.
/// </summary>
public sealed class ConsoleRegistryGuardBrowserTests
    : IClassFixture<ApiHostFixture>,
        IClassFixture<PasskeyBrowserFixture>
{
    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public ConsoleRegistryGuardBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_form_says_that_the_name_it_is_given_in_the_other_script_is_registered_and_that_a_spelling_of_it_is_like_one_that_is()
    {
        var mongo = _host.MongoConnectionString;
        var token = Guid.NewGuid().ToString("N")[..8];
        var (tenant, mainOperator) = await MainOperatorAsync();
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        await RegistrySeed.AthleteAsync(mongo, tenant, $"Юлия Тестова {token}", bulgaria, null, "10012345");
        await RegistrySeed.HorseAsync(mongo, tenant, $"Тест Огън {token}", null, "103BK17");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator, "/console/registry/athletes-horses");

        await AddToListAsync(page, "Athletes");
        var dialog = page.GetByRole(AriaRole.Dialog);
        var alert = dialog.GetByTestId("console-same-rows");
        var name = dialog.GetByLabel("Name", new() { Exact = true });

        // the name in Latin letters, which is the name by the official transliteration
        await name.FillAsync($"Yulia Testova {token}");
        await name.PressAsync("Tab");
        await Expect(alert).ToContainTextAsync($"Юлия Тестова {token}", Visible());
        await Expect(alert).ToContainTextAsync("the same name");

        // a spelling of it that only the looser key makes the same
        await name.FillAsync($"Iuliia Testova {token}");
        await name.PressAsync("Tab");
        await Expect(alert).ToContainTextAsync($"Юлия Тестова {token}", Visible());
        await Expect(alert).ToContainTextAsync("a similar name");

        // one letter off is a near-miss, which is no warning
        await name.FillAsync($"Yulia Testov {token}");
        await name.PressAsync("Tab");
        await Expect(alert).ToHaveCountAsync(0, Counted());
    }

    [Fact]
    public async Task A_form_says_that_the_name_a_Horse_is_given_in_the_other_script_is_registered()
    {
        var mongo = _host.MongoConnectionString;
        var token = Guid.NewGuid().ToString("N")[..8];
        var (tenant, mainOperator) = await MainOperatorAsync();
        await RegistrySeed.HorseAsync(mongo, tenant, $"Тест Огън {token}", null, "103BK17");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator, "/console/registry/athletes-horses");

        await AddToListAsync(page, "Horses");
        var dialog = page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Name", new() { Exact = true }).FillAsync($"Test Ogan {token}");
        await dialog.GetByLabel("Name", new() { Exact = true }).PressAsync("Tab");

        await Expect(dialog.GetByTestId("console-same-rows")).ToContainTextAsync($"Тест Огън {token}", Visible());
        await Expect(dialog.GetByTestId("console-same-rows")).ToContainTextAsync("the same name");
    }

    [Fact]
    public async Task A_row_that_a_Participation_refers_to_is_not_deleted_and_the_person_is_told_why_and_a_row_that_none_does_goes()
    {
        var mongo = _host.MongoConnectionString;
        var token = Guid.NewGuid().ToString("N")[..8];
        var (tenant, mainOperator) = await MainOperatorAsync();
        var eventId = await EventSeed.LiveAsync(mongo, tenant, null, DateTimeOffset.UtcNow);
        var participation = IntegrationPayloadFactory.ActiveParticipation(eventId, 1, Guid.NewGuid());
        await EventSeed.ParticipationAsync(mongo, tenant, participation);
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var named = await RegistrySeed.AthleteAsync(
            mongo,
            tenant,
            $"Named Rider {token}",
            bulgaria,
            id: participation.Combination.Athlete.Id
        );
        var free = await RegistrySeed.AthleteAsync(mongo, tenant, $"Free Rider {token}", bulgaria);
        var namedHorse = await RegistrySeed.HorseAsync(
            mongo,
            tenant,
            $"Named Horse {token}",
            id: participation.Combination.Horse.Id
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        page.SetDefaultTimeout(PATIENCE);
        await SignInAndOpenAsync(_host, device, mainOperator, "/console/registry/athletes-horses");
        await Expect(RowOf(page, $"Named Rider {token}")).ToBeVisibleAsync(Loaded());

        await DeleteAsync(page, $"Named Rider {token}");
        await Expect(page.GetByText("A Participation still names the Athlete.").First).ToBeVisibleAsync(Loaded());
        await DeleteAsync(page, $"Named Horse {token}");
        await Expect(page.GetByText("A Participation still names the Horse.").First).ToBeVisibleAsync(Loaded());
        await DeleteAsync(page, $"Free Rider {token}");

        await Expect(RowOf(page, $"Free Rider {token}")).ToHaveCountAsync(0, Counted());
        Assert.Equal(1, await RegistrySeed.Collection(mongo, "athletes").CountDocumentsAsync(Id(named)));
        Assert.Equal(1, await RegistrySeed.Collection(mongo, "horses").CountDocumentsAsync(Id(namedHorse)));
        Assert.Equal(0, await RegistrySeed.Collection(mongo, "athletes").CountDocumentsAsync(Id(free)));
        await Expect(RowOf(page, $"Named Rider {token}")).ToHaveCountAsync(1, Counted()); // the row that was refused is still listed
        await Expect(RowOf(page, $"Named Horse {token}")).ToHaveCountAsync(1, Counted());
    }

    async Task<(string Tenant, string Email)> MainOperatorAsync()
    {
        var mongo = _host.MongoConnectionString;
        var tenant = await TenancySeed.TenantAsync(mongo, withCountry: true);
        var mainOperator = await TenancySeed.AccountAsync(mongo, tenant, name: "Boris Ivanov");
        await EventSeed.SetupAsync(mongo, tenant, mainOperator.Id, "The Cup");
        return (tenant, mainOperator.Email);
    }

    static async Task DeleteAsync(IPage page, string name)
    {
        await RowOf(page, name).GetByRole(AriaRole.Button).Last.ClickAsync();
        await page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })
            .ClickAsync();
    }

    /// <summary>The line of a list that shows the row by its name: the name, and the buttons that change it.</summary>
    static ILocator RowOf(IPage page, string name)
    {
        return page.Locator(".border-solid").Filter(new() { HasText = name });
    }

    static BsonDocument Id(Guid id)
    {
        return new BsonDocument("_id", RegistrySeed.Binary(id));
    }

    static LocatorAssertionsToContainTextOptions Visible()
    {
        return new LocatorAssertionsToContainTextOptions { Timeout = PATIENCE };
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
