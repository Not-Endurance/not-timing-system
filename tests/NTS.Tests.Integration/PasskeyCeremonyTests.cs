using System.Buffers.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Email;
using NoTiming.Api.Features.Account;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The passkey ceremonies with a real browser (#600, ADR-0002): Chromium with a virtual authenticator, the real pages of
/// the Api and their script, on a host listening on a real port. The conditional UI of the email field cannot be
/// automated, so these run the same routes through the button's modal prompt; the autofill goes on the manual device
/// checklist.
/// </summary>
public sealed class PasskeyCeremonyTests : IClassFixture<ApiHostFixture>, IClassFixture<PasskeyBrowserFixture>
{
    // The Ui is a heavy page to load for a test that only wants to know it was reached: the health endpoint will do.
    const string AFTER_SIGN_IN = "/sign-in?returnUrl=/healthz";
    static readonly AccountText TEXT = AccountText.Load();

    readonly ApiHostFixture _host;
    readonly PasskeyBrowserFixture _browsers;

    public PasskeyCeremonyTests(ApiHostFixture host, PasskeyBrowserFixture browsers)
    {
        _host = host;
        _browsers = browsers;
    }

    [Fact]
    public async Task A_person_signs_in_with_a_code_is_offered_a_passkey_adds_it_signs_out_and_signs_back_in_with_it()
    {
        var email = await SeedUser();
        await using var device = await _browsers.OpenAsync(_host.BaseAddress);
        var page = device.Page;

        await SignInWithCodeAsync(device, email, AFTER_SIGN_IN);

        // A code sign-in with no passkey yet is followed by the offer to add one.
        await page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        await Expect(page.Locator("#offer-text")).ToHaveTextAsync(TEXT.Get("en", "passkeys.offer"));
        await page.ClickAsync("#add");

        await Expect(page.Locator("ul#list li")).ToHaveCountAsync(1);
        await Expect(page.Locator("#offer-text")).ToHaveTextAsync(TEXT.Get("en", "passkeys.offerSecond"));
        var added = Assert.Single(
            _host.Api.Services.GetRequiredService<IEmailOutbox>().Messages,
            x => x.Subject == TEXT.Get("en", "email.passkey.subject") && x.To == email
        );
        Assert.Equal("en", added.Language);

        Assert.Equal(204, (await device.FetchAsync("DELETE", "/api/sessions/current")).Status);
        Assert.Equal(401, (await device.FetchAsync("GET", "/api/me")).Status);

        await page.GotoAsync(device.Url(AFTER_SIGN_IN));
        await page.ClickAsync("#passkey");
        await page.WaitForURLAsync(device.Url("/healthz"));
        var me = await device.FetchAsync("GET", "/api/me");
        Assert.Equal(200, me.Status);
        Assert.Equal(email, me.Body.GetProperty("data").GetProperty("attributes").GetProperty("email").GetString());

        // No password was created at any point.
        var stored = await Users().Find(new BsonDocument("Email", email)).SingleAsync();
        Assert.False(stored.Contains("PasswordHash"));
        Assert.Single(stored["Passkeys"].AsBsonArray);
    }

    [Fact]
    public async Task Signing_in_three_times_with_a_passkey_leaves_one_credential_entry_and_moves_its_sign_count()
    {
        var email = await SeedUser();
        await using var device = await SignedInDeviceAsync(email);
        await AddPasskeyAsync(device, expected: 1);

        for (var time = 0; time < 3; time++)
        {
            await SignOutAsync(device);
            await SignInWithPasskeyAsync(device);
        }

        var stored = Assert.Single(await StoredPasskeysAsync(email));
        var held = Assert.Single(await device.CredentialsAsync());
        Assert.Equal(held.CredentialId, stored["CredentialId"].AsByteArray); // replaced in place, never added again
        Assert.True(held.SignCount >= 3);
        Assert.Equal(held.SignCount, (uint)stored["SignCount"].ToInt64()); // and the host kept the authenticator's count
    }

    [Fact]
    public async Task Passkeys_added_at_the_same_moment_on_two_devices_both_persist()
    {
        var email = await SeedUser();
        await using var first = await SignedInDeviceAsync(email);
        await using var second = await SignedInDeviceAsync(email);

        await Task.WhenAll(first.Page.ClickAsync("#add"), second.Page.ClickAsync("#add"));

        // Each page lists a passkey only after its own request was answered, so both have been stored by now.
        await Expect(first.Page.Locator("ul#list li").First).ToBeVisibleAsync();
        await Expect(second.Page.Locator("ul#list li").First).ToBeVisibleAsync();
        var stored = await StoredPasskeysAsync(email);
        Assert.Equal(
            (await first.CredentialsAsync()).Concat(await second.CredentialsAsync()).Select(x => x.Id).Order(),
            stored.Select(x => Base64Url.EncodeToString(x["CredentialId"].AsByteArray)).Order()
        );
        await first.Page.ReloadAsync();
        await Expect(first.Page.Locator("ul#list li")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task A_passkey_that_was_removed_can_no_longer_sign_in()
    {
        var email = await SeedUser();
        await using var phone = await SignedInDeviceAsync(email);
        await AddPasskeyAsync(phone, expected: 1);
        await using var laptop = await SignedInDeviceAsync(email);
        await AddPasskeyAsync(laptop, expected: 2);
        var phonePasskey = Assert.Single(await phone.CredentialsAsync()).Id;

        laptop.Page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await laptop
            .Page.Locator($"li[data-id='{phonePasskey}']")
            .GetByRole(
                AriaRole.Button,
                new LocatorGetByRoleOptions { Name = TEXT.Get("en", "passkeys.remove"), Exact = true }
            )
            .ClickAsync();
        await Expect(laptop.Page.Locator("ul#list li")).ToHaveCountAsync(1);

        await SignOutAsync(phone);
        await phone.Page.GotoAsync(phone.Url(AFTER_SIGN_IN));
        await phone.Page.ClickAsync("#passkey"); // the phone still holds the credential, and presents it
        await Expect(phone.Page.Locator("#message")).ToHaveTextAsync(TEXT.Get("en", "error.passkey"));
        Assert.Equal(401, (await phone.FetchAsync("GET", "/api/me")).Status);
        await laptop.Page.ReloadAsync();
        await SignOutAsync(laptop);
        await SignInWithPasskeyAsync(laptop); // the passkey that was kept still works
    }

    [Fact]
    public async Task The_last_passkey_cannot_be_removed()
    {
        var email = await SeedUser();
        await using var device = await SignedInDeviceAsync(email);
        await AddPasskeyAsync(device, expected: 1);

        device.Page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await device
            .Page.Locator("ul#list li")
            .GetByRole(
                AriaRole.Button,
                new LocatorGetByRoleOptions { Name = TEXT.Get("en", "passkeys.remove"), Exact = true }
            )
            .ClickAsync();

        await Expect(device.Page.Locator("#message")).ToHaveTextAsync(TEXT.Get("en", "passkeys.error.last"));
        Assert.Single(await StoredPasskeysAsync(email));
        await device.Page.ReloadAsync();
        await Expect(device.Page.Locator("ul#list li")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Rotating_the_security_stamp_ends_a_session_that_was_made_with_a_passkey()
    {
        var email = await SeedUser();
        await using var device = await SignedInDeviceAsync(email);
        await AddPasskeyAsync(device, expected: 1);
        await SignOutAsync(device);
        await SignInWithPasskeyAsync(device);
        Assert.Equal(200, (await device.FetchAsync("GET", "/api/me")).Status);

        using (var scope = _host.Api.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.True((await users.UpdateSecurityStampAsync(user!)).Succeeded);
        }

        Assert.Equal(401, (await device.FetchAsync("GET", "/api/me")).Status);
    }

    [Fact]
    public async Task A_second_passkey_is_offered_after_every_code_sign_in_until_there_are_two()
    {
        var email = await SeedUser();
        await using (var first = await SignedInDeviceAsync(email))
        {
            await AddPasskeyAsync(first, expected: 1);
        }

        await using var second = await SignedInDeviceAsync(email); // a code sign-in, with one passkey so far
        await Expect(second.Page.Locator("#offer-text")).ToHaveTextAsync(TEXT.Get("en", "passkeys.offerSecond"));
        await AddPasskeyAsync(second, expected: 2);
        await SignOutAsync(second);

        await SignInWithCodeAsync(second, email, AFTER_SIGN_IN);
        await second.Page.WaitForURLAsync(second.Url("/healthz")); // two passkeys: there is nothing left to offer
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("bg-BG", "bg")]
    [InlineData("tr-TR", "tr")]
    public async Task The_mail_about_a_new_passkey_is_in_the_language_of_the_browser(string locale, string language)
    {
        var email = await SeedUser();
        await using var device = await SignedInDeviceAsync(email, locale);

        await AddPasskeyAsync(device, expected: 1);

        var mail = Assert.Single(
            _host.Api.Services.GetRequiredService<IEmailOutbox>().Messages,
            x => x.To == email && x.Subject == TEXT.Get(language, "email.passkey.subject")
        );
        Assert.Equal(language, mail.Language);
        Assert.Contains(TEXT.Get(language, "email.passkey.body").Split("\n\n")[1], mail.TextBody);
    }

    static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }

    /// <summary>Adds a passkey on the passkeys page and waits until the list shows the number expected.</summary>
    static async Task AddPasskeyAsync(BrowserDevice device, int expected)
    {
        await device.Page.ClickAsync("#add");
        await Expect(device.Page.Locator("ul#list li")).ToHaveCountAsync(expected);
    }

    static async Task SignOutAsync(BrowserDevice device)
    {
        Assert.Equal(204, (await device.FetchAsync("DELETE", "/api/sessions/current")).Status);
    }

    static async Task SignInWithPasskeyAsync(BrowserDevice device)
    {
        await device.Page.GotoAsync(device.Url(AFTER_SIGN_IN));
        await device.Page.ClickAsync("#passkey");
        await device.Page.WaitForURLAsync(device.Url("/healthz"));
    }

    /// <summary>
    /// A device whose person has just signed in with an emailed code, which is what lands them on the passkeys page
    /// while they hold fewer than two.
    /// </summary>
    async Task<BrowserDevice> SignedInDeviceAsync(string email, string locale = "en-US")
    {
        var device = await _browsers.OpenAsync(_host.BaseAddress, locale);
        await SignInWithCodeAsync(device, email, AFTER_SIGN_IN);
        await device.Page.WaitForURLAsync(new Regex(@"/account/passkeys\?offer=1"));
        return device;
    }

    async Task SignInWithCodeAsync(BrowserDevice device, string email, string path)
    {
        await device.Page.GotoAsync(device.Url(path));
        await device.Page.FillAsync("#email", email);
        await device.Page.ClickAsync("#email-form button[type=submit]");
        await device.Page.WaitForSelectorAsync("#code-form:not([hidden])");
        await device.Page.FillAsync("#code", ApiSessions.CodeSentTo(_host.Api, email));
        await device.Page.ClickAsync("#code-form button[type=submit]");
    }

    async Task<IReadOnlyList<BsonDocument>> StoredPasskeysAsync(string email)
    {
        var stored = await Users().Find(new BsonDocument("Email", email)).SingleAsync();
        return [.. stored["Passkeys"].AsBsonArray.Select(x => x.AsBsonDocument)];
    }

    async Task<string> SeedUser()
    {
        var email = UserSeed.NewEmail("ceremony");
        await UserSeed.AddLegacyUserAsync(_host.MongoConnectionString, email);
        return email;
    }

    IMongoCollection<BsonDocument> Users()
    {
        return UserSeed.Users(_host.MongoConnectionString);
    }
}
