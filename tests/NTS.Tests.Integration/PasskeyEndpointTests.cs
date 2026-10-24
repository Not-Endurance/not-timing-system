using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity.Email;
using NoTiming.Api.Features.Account;
using NTS.Tests.Integration.Infrastructure;
using Xunit.Abstractions;

namespace NTS.Tests.Integration;

/// <summary>
/// The passkey routes without a browser (#600): what they require, what they refuse and what the options ask of an
/// authenticator. The ceremonies themselves, with a real authenticator, are in <c>PasskeyCeremonyTests</c>.
/// </summary>
public sealed class PasskeyEndpointTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;
    readonly ITestOutputHelper _output;

    public PasskeyEndpointTests(MongoFixture mongo, ITestOutputHelper output)
    {
        _mongo = mongo;
        _output = output;
    }

    [Fact]
    public async Task A_page_carries_an_antiforgery_token_and_sets_its_cookie()
    {
        await using var api = NewApi();
        var page = new PageClient(ClientOf(api));

        var html = await page.OpenAsync("/sign-in");

        Assert.False(string.IsNullOrEmpty(PageClient.AntiforgeryTokenOf(html)));
        Assert.NotNull(page.Cookie(PasskeyServices.ANTIFORGERY_COOKIE_DEVELOPMENT));
        Assert.Contains("data-passkeys=\"true\"", html);
    }

    [Fact]
    public async Task Outside_development_the_antiforgery_cookie_is_host_only_and_secure()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString, environment: "Staging");
        using var client = ClientOf(api);

        using var response = await client.GetAsync("/sign-in");

        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            x => x.StartsWith(PasskeyServices.ANTIFORGERY_COOKIE + "=", StringComparison.Ordinal)
        );
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task In_development_over_plain_http_the_page_still_opens()
    {
        await using var api = NewApi();
        using var client = api.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") }
        );

        using var response = await client.GetAsync("/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_options_for_creating_a_passkey_need_a_signed_in_person_and_an_antiforgery_token()
    {
        await using var api = NewApi();
        var page = await SignedInAsync(api);

        var withToken = await page.PostAsync("/api/passkeys/actions/creation-options");
        var withoutToken = await page.PostAsync("/api/passkeys/actions/creation-options", withToken: false);
        var anonymous = new PageClient(ClientOf(api));
        await anonymous.OpenAsync("/sign-in");
        var notSignedIn = await anonymous.PostAsync("/api/passkeys/actions/creation-options");

        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        Assert.Equal("antiforgery-token-invalid", await ErrorCodeOf(withoutToken));
        Assert.Equal(HttpStatusCode.Unauthorized, notSignedIn.StatusCode);
        Assert.Equal("not-signed-in", await ErrorCodeOf(notSignedIn));
    }

    [Fact]
    public async Task The_options_ask_for_a_discoverable_passkey_with_user_verification_and_no_attestation()
    {
        await using var api = NewApi();
        var page = await SignedInAsync(api);

        var response = await page.PostAsync("/api/passkeys/actions/creation-options");
        var options = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("options");
        _output.WriteLine(options.GetRawText());

        Assert.Equal("localhost", options.GetProperty("rp").GetProperty("id").GetString());
        Assert.False(string.IsNullOrEmpty(options.GetProperty("challenge").GetString()));
        var selection = options.GetProperty("authenticatorSelection");
        Assert.Equal("required", selection.GetProperty("residentKey").GetString());
        Assert.Equal("required", selection.GetProperty("userVerification").GetString());
        Assert.Equal("none", options.GetProperty("attestation").GetString());
    }

    [Fact]
    public async Task The_options_for_signing_in_name_no_account()
    {
        await using var api = NewApi();
        var page = new PageClient(ClientOf(api));
        await page.OpenAsync("/sign-in");

        var response = await page.PostAsync("/api/passkeys/actions/request-options");
        var options = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("options");
        _output.WriteLine(options.GetRawText());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("localhost", options.GetProperty("rpId").GetString());
        Assert.Equal("required", options.GetProperty("userVerification").GetString());
        Assert.False(options.TryGetProperty("allowCredentials", out var allowed) && allowed.GetArrayLength() > 0);
    }

    [Fact]
    public async Task The_options_for_signing_in_need_the_antiforgery_token_too()
    {
        await using var api = NewApi();
        var page = new PageClient(ClientOf(api));
        await page.OpenAsync("/sign-in");

        var response = await page.PostAsync("/api/passkeys/actions/request-options", withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("antiforgery-token-invalid", await ErrorCodeOf(response));
    }

    [Fact]
    public async Task The_routes_that_act_for_a_person_need_that_person_signed_in()
    {
        await using var api = NewApi();
        var page = new PageClient(ClientOf(api));
        await page.OpenAsync("/sign-in");

        var list = await page.GetAsync("/api/passkeys");
        var rename = await page.WriteAsync(HttpMethod.Patch, "/api/passkeys/abc", "passkeys", new { name = "x" });
        var remove = await page.DeleteAsync("/api/passkeys/abc");
        var create = await page.WriteAsync(HttpMethod.Post, "/api/passkeys", "passkeys", new { credential = new { } });

        Assert.All(
            [list, rename, remove, create],
            response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode)
        );
    }

    [Fact]
    public async Task A_credential_that_is_not_one_is_refused_for_the_person_and_for_the_session()
    {
        await using var api = NewApi();
        var page = await SignedInAsync(api);

        var create = await page.WriteAsync(
            HttpMethod.Post,
            "/api/passkeys",
            "passkeys",
            new { credential = new { id = "x", type = "public-key" } }
        );
        var noCredential = await page.WriteAsync(
            HttpMethod.Post,
            "/api/passkeys",
            "passkeys",
            new { name = "no credential" }
        );
        var anonymous = new PageClient(ClientOf(api));
        await anonymous.OpenAsync("/sign-in");
        var session = await anonymous.WriteAsync(
            HttpMethod.Post,
            "/api/sessions",
            "sessions",
            new { credential = new { id = "x", type = "public-key" } }
        );

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal("invalid-passkey", await ErrorCodeOf(create));
        Assert.Equal(HttpStatusCode.BadRequest, noCredential.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal("invalid-passkey", await ErrorCodeOf(session));
    }

    [Fact]
    public async Task A_credential_posted_for_a_session_needs_the_antiforgery_token_a_code_does_not()
    {
        await using var api = NewApi();
        var anonymous = new PageClient(ClientOf(api));
        await anonymous.OpenAsync("/sign-in");

        var withoutToken = await anonymous.WriteAsync(
            HttpMethod.Post,
            "/api/sessions",
            "sessions",
            new { credential = new { id = "x" } },
            withToken: false
        );
        var code = await anonymous.WriteAsync(
            HttpMethod.Post,
            "/api/sessions",
            "sessions",
            new { email = UserSeed.NewEmail("nobody"), code = "123456" },
            withToken: false
        );

        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        Assert.Equal("antiforgery-token-invalid", await ErrorCodeOf(withoutToken));
        Assert.Equal(HttpStatusCode.Unauthorized, code.StatusCode);
        Assert.Equal("invalid-code", await ErrorCodeOf(code));
    }

    [Fact]
    public async Task A_host_without_a_relying_party_id_offers_no_passkeys_and_still_signs_in_with_a_code()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString, environment: "Staging");
        var page = new PageClient(ClientOf(api));
        var email = UserSeed.NewEmail("staging");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);

        var html = await page.OpenAsync("/sign-in");
        var options = await page.PostAsync("/api/passkeys/actions/request-options");
        var cookie = await ApiSessions.SignInAsync(api, ClientOf(api), email);
        page.Set(cookie);
        var creation = await page.PostAsync("/api/passkeys/actions/creation-options");

        Assert.Contains("data-passkeys=\"false\"", html);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, options.StatusCode);
        Assert.Equal("passkeys-not-configured", await ErrorCodeOf(options));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, creation.StatusCode);
    }

    [Fact]
    public async Task The_relying_party_id_is_configuration_not_code()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            environment: "Staging",
            configureHost: builder => builder.UseSetting("Passkeys:RelyingPartyId", "Staging.Example.Test")
        );
        var page = new PageClient(ClientOf(api));
        await page.OpenAsync("/sign-in");

        var response = await page.PostAsync("/api/passkeys/actions/request-options");
        var options = (await ApiSessions.ReadJsonAsync(response))
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("options");

        Assert.Equal("staging.example.test", options.GetProperty("rpId").GetString());
    }

    [Fact]
    public async Task A_passkey_made_without_a_browser_is_stored_listed_and_announced_by_mail()
    {
        await using var api = NewApi();
        var email = UserSeed.NewEmail("announce");
        var page = await SignedInAsAsync(api, email);
        var credentialId = RandomNumberGenerator.GetBytes(32);

        var created = await SoftwareAuthenticator.AddPasskeyAsync(page, credentialId, "Work laptop");
        var listed = await ApiSessions.ReadJsonAsync(await page.GetAsync("/api/passkeys"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var passkey = Assert.Single(listed.GetProperty("data").EnumerateArray());
        Assert.Equal(Base64Url.EncodeToString(credentialId), passkey.GetProperty("id").GetString());
        Assert.Equal("Work laptop", passkey.GetProperty("attributes").GetProperty("name").GetString());
        var mail = Assert.Single(
            api.Services.GetRequiredService<IEmailOutbox>().Messages,
            x => x.To == email && x.Subject == AccountText.Load().Get("en", "email.passkey.subject")
        );
        Assert.Contains("\"Work laptop\"", mail.TextBody);
    }

    [Fact]
    public async Task A_passkey_is_added_even_when_the_mail_about_it_cannot_be_sent()
    {
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureServices: services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender, OutboxThatCannotSendPasskeyMail>();
            }
        );
        var page = await SignedInAsAsync(api, UserSeed.NewEmail("mailfails"));

        var created = await SoftwareAuthenticator.AddPasskeyAsync(page, RandomNumberGenerator.GetBytes(32), "Phone");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Single(await ListAsync(page)); // it is stored, and the page is told so
    }

    [Fact]
    public async Task A_passkey_name_of_more_than_sixty_characters_is_refused_and_nothing_is_stored()
    {
        await using var api = NewApi();
        var email = UserSeed.NewEmail("longname");
        var page = await SignedInAsAsync(api, email);

        var tooLong = await SoftwareAuthenticator.AddPasskeyAsync(
            page,
            RandomNumberGenerator.GetBytes(32),
            new string('x', 61)
        );
        var longest = await SoftwareAuthenticator.AddPasskeyAsync(
            page,
            RandomNumberGenerator.GetBytes(32),
            new string('y', 60)
        );

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("invalid-name", await ErrorCodeOf(tooLong));
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        var stored = await UserSeed.Users(_mongo.ConnectionString).Find(new BsonDocument("Email", email)).SingleAsync();
        Assert.Single(stored["Passkeys"].AsBsonArray); // only the one that was accepted
    }

    [Fact]
    public async Task A_passkey_is_renamed_and_a_name_of_more_than_sixty_characters_is_refused()
    {
        await using var api = NewApi();
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var page = await SignedInAsAsync(api, UserSeed.NewEmail("rename"), credentialId);
        var path = "/api/passkeys/" + Base64Url.EncodeToString(credentialId);

        var renamed = await page.WriteAsync(HttpMethod.Patch, path, "passkeys", new { name = "Kitchen tablet" });
        var tooLong = await page.WriteAsync(HttpMethod.Patch, path, "passkeys", new { name = new string('z', 61) });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Kitchen tablet", await NameOf(renamed));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal("invalid-name", await ErrorCodeOf(tooLong));
        Assert.Equal("Kitchen tablet", NameOf(Assert.Single(await ListAsync(page)))); // the refusal changed nothing
    }

    [Fact]
    public async Task A_person_cannot_rename_or_remove_the_passkey_of_someone_else()
    {
        await using var api = NewApi();
        var victimsPasskey = RandomNumberGenerator.GetBytes(32);
        var victim = UserSeed.NewEmail("victim");
        await SignedInAsAsync(api, victim, victimsPasskey);
        // Two of their own, so that a removal of someone else's could not be mistaken for the last-passkey rule.
        var page = await SignedInAsAsync(
            api,
            UserSeed.NewEmail("other"),
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32)
        );
        var theirs = "/api/passkeys/" + Base64Url.EncodeToString(victimsPasskey);
        var nobodys = "/api/passkeys/" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var rename = await page.WriteAsync(HttpMethod.Patch, theirs, "passkeys", new { name = "Mine now" });
        var remove = await page.DeleteAsync(theirs);
        var renameUnknown = await page.WriteAsync(HttpMethod.Patch, nobodys, "passkeys", new { name = "x" });
        var removeUnknown = await page.DeleteAsync(nobodys);
        var removeMalformed = await page.DeleteAsync("/api/passkeys/not%20base64!");

        Assert.All(
            [rename, remove, renameUnknown, removeUnknown, removeMalformed],
            response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
        );
        var kept = await UserSeed.Users(_mongo.ConnectionString).Find(new BsonDocument("Email", victim)).SingleAsync();
        var passkey = Assert.Single(kept["Passkeys"].AsBsonArray).AsBsonDocument;
        Assert.StartsWith("Stored ", passkey["Name"].AsString);
    }

    [Fact]
    public async Task A_passkey_is_removed_while_another_remains_and_the_last_one_is_kept()
    {
        await using var api = NewApi();
        var first = RandomNumberGenerator.GetBytes(32);
        var second = RandomNumberGenerator.GetBytes(32);
        var page = await SignedInAsAsync(api, UserSeed.NewEmail("remove"), first, second);

        var removed = await page.DeleteAsync("/api/passkeys/" + Base64Url.EncodeToString(first));
        var last = await page.DeleteAsync("/api/passkeys/" + Base64Url.EncodeToString(second));

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
        Assert.Equal("last-passkey", await ErrorCodeOf(last));
        var remaining = Assert.Single(await ListAsync(page));
        Assert.Equal(Base64Url.EncodeToString(second), remaining.GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("/\t/evil.example")] // a browser removes the tab, and what is left is //evil.example
    [InlineData("/\n/evil.example")]
    [InlineData("/\r/evil.example")]
    [InlineData("/\t\\evil.example")]
    [InlineData("/path\0/with-a-control-character")]
    public async Task A_return_path_with_a_control_character_is_not_used(string requested)
    {
        await using var api = NewApi();
        using var client = ClientOf(api);

        var html = await client.GetStringAsync("/sign-in?returnUrl=" + Uri.EscapeDataString(requested));

        Assert.Contains("data-return-url=\"/\"", html);
    }

    [Fact]
    public async Task The_passkeys_page_leads_back_only_to_a_path_of_the_site()
    {
        await using var api = NewApi();
        var page = await SignedInAsync(api);

        var html = await page.OpenAsync("/account/passkeys?returnUrl=" + Uri.EscapeDataString("/\t/evil.example"));

        Assert.Contains("id=\"done\" class=\"link\" href=\"/\"", html);
        Assert.Contains("data-return-url=\"/\"", html);
    }

    [Theory]
    [InlineData("https://localhost", "localhost", true)]
    [InlineData("http://localhost:5000", "localhost", true)]
    [InlineData("https://app.example.test", "example.test", true)]
    [InlineData("https://example.test", "example.test", true)]
    [InlineData("https://app.staging.example.test", "staging.example.test", true)]
    [InlineData("http://app.example.test", "example.test", false)]
    [InlineData("https://evil-example.test", "example.test", false)]
    [InlineData("https://example.test.evil.example", "example.test", false)]
    [InlineData("https://app.example.test", "staging.example.test", false)]
    [InlineData("http://127.0.0.1:5000", "localhost", false)]
    [InlineData("not an origin", "example.test", false)]
    [InlineData(null, "example.test", false)]
    public void An_origin_may_use_a_relying_party_only_inside_its_domain_over_https(
        string? origin,
        string relyingPartyId,
        bool allowed
    )
    {
        Assert.Equal(allowed, PasskeyServices.IsAllowedOrigin(origin, relyingPartyId));
    }

    static HttpClient ClientOf(ApiFactory api)
    {
        return api.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
    }

    static async Task<string?> ErrorCodeOf(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.TryGetProperty("errors", out var errors) ? errors[0].GetProperty("code").GetString() : null;
    }

    /// <summary>The name of the passkey a response carries (a rename answers with it).</summary>
    static async Task<string?> NameOf(HttpResponseMessage response)
    {
        return NameOf((await ApiSessions.ReadJsonAsync(response)).GetProperty("data"));
    }

    static string? NameOf(JsonElement passkey)
    {
        return passkey.GetProperty("attributes").GetProperty("name").GetString();
    }

    static async Task<IReadOnlyList<JsonElement>> ListAsync(PageClient page)
    {
        var body = await ApiSessions.ReadJsonAsync(await page.GetAsync("/api/passkeys"));
        return [.. body.GetProperty("data").EnumerateArray()];
    }

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString);
    }

    Task<PageClient> SignedInAsync(ApiFactory api)
    {
        return SignedInAsAsync(api, UserSeed.NewEmail("passkeys"));
    }

    /// <summary>A person who has signed in with a code and opened their passkeys, optionally with some already stored.</summary>
    async Task<PageClient> SignedInAsAsync(ApiFactory api, string email, params byte[][] storedPasskeys)
    {
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document =>
            {
                if (storedPasskeys.Length > 0)
                {
                    document["Passkeys"] = new BsonArray(storedPasskeys.Select(x => UserSeed.StoredPasskey(x)));
                }
            }
        );
        var page = new PageClient(ClientOf(api));
        page.Set(await ApiSessions.SignInAsync(api, ClientOf(api), email));
        await page.OpenAsync("/account/passkeys");
        return page;
    }

    /// <summary>A mail provider that is down for exactly one mail: the one about a passkey. The rest goes to the outbox.</summary>
    sealed class OutboxThatCannotSendPasskeyMail : IDevelopmentEmailSender, IEmailOutbox
    {
        readonly OutboxEmailSender _outbox = new();

        public IReadOnlyList<EmailMessage> Messages => _outbox.Messages;

        public void Clear()
        {
            _outbox.Clear();
        }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return message.Subject == AccountText.Load().Get("en", "email.passkey.subject")
                ? throw new InvalidOperationException("The mail provider is down.")
                : _outbox.SendAsync(message, cancellationToken);
        }
    }
}
