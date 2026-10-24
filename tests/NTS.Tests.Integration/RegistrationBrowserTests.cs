using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// A new person registers in a real browser (#601, ADR-0002): the page of the Api, its script, the emailed code, the
/// offer of a passkey that a virtual authenticator answers, and then the passkey signs them in again. Like the passkey
/// ceremonies these need Chromium and a host on a real port; the code is read from the outbox of the host.
/// </summary>
public sealed class RegistrationBrowserTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    // The Ui is a heavy page to load for a test that only wants to know it was reached: the health endpoint will do.
    const string AFTER_SIGN_IN = "/healthz";

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public RegistrationBrowserTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_new_person_registers_is_offered_a_passkey_adds_it_signs_out_and_signs_back_in_with_it()
    {
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_host.MongoConnectionString, $"Browserland {iso}", iso);
        var email = UserSeed.NewEmail("browser");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;

        await page.GotoAsync(device.Url($"/register?returnUrl={AFTER_SIGN_IN}"));
        await Expect(page.Locator("#website")).Not.ToBeInViewportAsync(); // the trap is not where a person looks
        await page.FillAsync("#given-name", "Ana");
        await page.FillAsync("#surname", "Petrova");
        await page.FillAsync("#email", email);
        await page.SelectOptionAsync("#country", country.ToString());
        await page.ClickAsync("#register-form button[type=submit]");

        await page.WaitForSelectorAsync("#code-form:not([hidden])");
        Assert.Null(await FindUserAsync(email)); // nothing is stored until the code comes back
        await page.FillAsync("#code", ApiSessions.CodeSentTo(_host.Api, email));
        await page.ClickAsync("#code-form button[type=submit]");

        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        await page.ClickAsync("#add");
        await Expect(page.Locator("ul#list li")).ToHaveCountAsync(1);
        Assert.Equal(204, (await device.FetchAsync("DELETE", "/api/sessions/current")).Status);
        Assert.Equal(401, (await device.FetchAsync("GET", "/api/me")).Status);

        await page.GotoAsync(device.Url($"/sign-in?returnUrl={AFTER_SIGN_IN}"));
        await page.ClickAsync("#passkey");
        await page.WaitForURLAsync(device.Url(AFTER_SIGN_IN));
        var me = await device.FetchAsync("GET", "/api/me");
        Assert.Equal(200, me.Status);
        Assert.Equal(
            "Ana Petrova",
            me.Body.GetProperty("data").GetProperty("attributes").GetProperty("name").GetString()
        );

        var user = (await FindUserAsync(email))!;
        Assert.Equal($"country-{iso.ToLowerInvariant()}", user["HomeTenantId"].AsString);
        Assert.Single(user["Memberships"].AsBsonArray);
        Assert.Single(user["Passkeys"].AsBsonArray);
        Assert.False(user.Contains("PasswordHash")); // no password was created at any point
    }

    [Fact]
    public async Task A_person_who_skips_the_passkey_is_registered_and_signed_in_and_goes_on_to_where_they_were_going()
    {
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_host.MongoConnectionString, $"Skipland {iso}", iso);
        var email = UserSeed.NewEmail("skipper");
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        await page.GotoAsync(device.Url($"/register?returnUrl={AFTER_SIGN_IN}"));
        await page.FillAsync("#given-name", "Boris");
        await page.FillAsync("#surname", "Ivanov");
        await page.FillAsync("#email", email);
        await page.SelectOptionAsync("#country", country.ToString());
        await page.ClickAsync("#register-form button[type=submit]");
        await page.WaitForSelectorAsync("#code-form:not([hidden])");
        await page.FillAsync("#code", ApiSessions.CodeSentTo(_host.Api, email));
        await page.ClickAsync("#code-form button[type=submit]");
        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));

        await page.ClickAsync("#done");

        await page.WaitForURLAsync(device.Url(AFTER_SIGN_IN));
        Assert.Equal(200, (await device.FetchAsync("GET", "/api/me")).Status);
        Assert.Empty((await FindUserAsync(email))!.GetValue("Passkeys", new BsonArray()).AsBsonArray);
    }

    [Fact]
    public async Task A_person_who_had_an_account_before_Tenants_is_placed_when_they_sign_in_with_a_passkey()
    {
        var iso = CountrySeed.UniqueIsoCode();
        var name = $"Passkeyland {iso}";
        await CountrySeed.AddAsync(_host.MongoConnectionString, name, iso);
        var email = UserSeed.NewEmail("legacy");
        await UserSeed.AddLegacyUserAsync(
            _host.MongoConnectionString,
            email,
            shape: document => document["CountryRegion"] = name
        );
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;
        await page.GotoAsync(device.Url($"/sign-in?returnUrl={AFTER_SIGN_IN}"));
        await page.FillAsync("#email", email);
        await page.ClickAsync("#email-form button[type=submit]");
        await page.WaitForSelectorAsync("#code-form:not([hidden])");
        await page.FillAsync("#code", ApiSessions.CodeSentTo(_host.Api, email));
        await page.ClickAsync("#code-form button[type=submit]");
        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        await page.ClickAsync("#add");
        await Expect(page.Locator("ul#list li")).ToHaveCountAsync(1);
        Assert.Equal(204, (await device.FetchAsync("DELETE", "/api/sessions/current")).Status);

        // The code sign-in placed them. Put them back as they were before Tenants, so that only the passkey can.
        await UserSeed
            .Users(_host.MongoConnectionString)
            .UpdateOneAsync(
                new BsonDocument("Email", email),
                Builders<BsonDocument>.Update.Unset("HomeTenantId").Unset("Memberships")
            );
        await page.GotoAsync(device.Url($"/sign-in?returnUrl={AFTER_SIGN_IN}"));
        await page.ClickAsync("#passkey");
        await page.WaitForURLAsync(device.Url(AFTER_SIGN_IN));

        var user = (await FindUserAsync(email))!;
        Assert.Equal($"country-{iso.ToLowerInvariant()}", user["HomeTenantId"].AsString);
        Assert.Single(user["Memberships"].AsBsonArray);
    }

    static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    async Task<BsonDocument?> FindUserAsync(string email)
    {
        return await ApiClients.FindUserAsync(_host.MongoConnectionString, email);
    }
}
