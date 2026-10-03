using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
        Assert.NotNull(page.Cookie("__Host-NoTiming-Xsrf"));
        Assert.Contains("data-passkeys=\"true\"", html);
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

    ApiFactory NewApi()
    {
        return new ApiFactory(_mongo.ConnectionString);
    }

    async Task<PageClient> SignedInAsync(ApiFactory api)
    {
        var email = UserSeed.NewEmail("passkeys");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        var page = new PageClient(ClientOf(api));
        page.Set(await ApiSessions.SignInAsync(api, ClientOf(api), email));
        await page.OpenAsync("/account/passkeys");
        return page;
    }
}
