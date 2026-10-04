using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Not.Identity;
using Not.Identity.Email;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The authentication events are logged (#601, ADR-0002): a code requested, verified or refused, a sign-in and a
/// sign-out, a passkey added or removed, the sessions of a user ended, a registration, a request that a limit refused.
/// Each is an event with an id and a name, carries the user and what happened, and carries nothing that could be used
/// to sign in: no code, no cookie, no token. The tests keep what the host logs and look for both.
/// </summary>
public sealed class AuthEventLogTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public AuthEventLogTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Signing_in_and_out_passkeys_and_the_ending_of_sessions_are_logged_with_the_user_and_no_secret()
    {
        var capture = new LogCapture();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = NewApi(capture, time);
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("events");
        var userId = await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);

        await ApiSessions.RequestCodeAsync(client, email);
        var code = ApiSessions.CodeSentTo(api, email);
        await ApiSessions.CreateSessionAsync(client, email, code == "000000" ? "000001" : "000000");
        var created = await ApiSessions.CreateSessionAsync(client, email, code);
        var cookie = SessionCookie.From(created)!;
        var page = new PageClient(client);
        page.Set(cookie);
        await page.OpenAsync("/account/passkeys");
        var first = RandomNumberGenerator.GetBytes(32);
        var second = RandomNumberGenerator.GetBytes(32);
        Assert.Equal(
            HttpStatusCode.Created,
            (await SoftwareAuthenticator.AddPasskeyAsync(page, first, "Laptop")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Created,
            (await SoftwareAuthenticator.AddPasskeyAsync(page, second, "Phone")).StatusCode
        );
        var removed = await page.DeleteAsync($"/api/passkeys/{Base64Url.EncodeToString(first)}");
        var signedOut = await page.DeleteAsync("/api/sessions/current");
        time.Advance(TimeSpan.FromMinutes(2));
        await ApiSessions.SignInAsync(api, client, email);
        await RotateTheSecurityStampAsync(api, userId);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        var user = userId.ToString();
        Assert.All(capture.Of("CodeRequested"), x => Assert.Contains(user, x.Message));
        Assert.Equal(2, capture.Of("CodeRequested").Count);
        Assert.Single(capture.Of("CodeRefused"));
        Assert.Equal(2, capture.Of("CodeVerified").Count);
        Assert.Equal(2, capture.Of("SignedIn").Count);
        Assert.Equal(2, capture.Of("PasskeyAdded").Count);
        Assert.Single(capture.Of("PasskeyRemoved"));
        Assert.Single(capture.Of("SignedOut"));
        Assert.NotEmpty(capture.Of("SessionsRevoked"));
        foreach (
            var name in new[]
            {
                "CodeVerified",
                "SignedIn",
                "PasskeyAdded",
                "PasskeyRemoved",
                "SignedOut",
                "SessionsRevoked",
            }
        )
        {
            Assert.All(capture.Of(name), x => Assert.Contains(user, x.Message));
        }

        AssertNoneSays(
            capture,
            code,
            cookie.Value,
            page.AntiforgeryToken!,
            Base64Url.EncodeToString(first),
            Base64Url.EncodeToString(second),
            email
        );
    }

    [Fact]
    public async Task A_registration_is_logged_and_so_is_the_code_for_an_address_that_has_no_account_yet()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.OfBrowser(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var email = UserSeed.NewEmail("registers");

        await ApiSessions.PostAsync(client, "/api/registrations", "registrations", Registration(email, country));
        var code = ApiSessions.CodeSentTo(api, email);
        var created = await ApiSessions.CreateSessionAsync(client, email, code);

        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!["_id"].AsGuid.ToString();
        Assert.Single(capture.Of("CodeRequested"));
        var registered = Assert.Single(capture.Of("Registered"));
        Assert.Contains(user, registered.Message);
        Assert.Contains(user, Assert.Single(capture.Of("SignedIn")).Message);
        AssertNoneSays(capture, code, SessionCookie.From(created)!.Value, email);
    }

    [Fact]
    public async Task A_request_that_a_limit_refuses_is_logged_with_the_limit_and_without_the_address_or_the_client()
    {
        var capture = new LogCapture();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            productionRateLimits: true,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("limited");

        for (var i = 0; i < 6; i++)
        {
            await ApiSessions.RequestCodeAsync(client, email, from: "10.20.0.1");
        }

        var refused = Assert.Single(capture.Of("RateLimited"));
        Assert.Equal(LogLevel.Warning, refused.Level);
        Assert.Contains("send/address", refused.Message);
        AssertNoneSays(capture, email, "10.20.0.1");
    }

    [Fact]
    public async Task A_flood_of_refused_requests_is_logged_once_a_minute_with_the_number_that_were_left_out()
    {
        var capture = new LogCapture();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            time: time,
            productionRateLimits: true,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("flood");

        for (var i = 0; i < 25; i++)
        {
            await ApiSessions.RequestCodeAsync(client, email, from: "10.30.0.1"); // five are let through, twenty refused
        }

        var firstMinute = capture.Of("RateLimited").Count;
        time.Advance(TimeSpan.FromSeconds(61));
        await ApiSessions.RequestCodeAsync(client, email, from: "10.30.0.1"); // the hour is not over: refused again

        var all = capture.Of("RateLimited");
        Assert.Equal(1, firstMinute);
        Assert.Equal(2, all.Count);
        Assert.Contains("19 refused since the last report", all[1].Message);
    }

    [Fact]
    public async Task Each_limit_is_reported_on_its_own_even_when_another_was_reported_a_moment_ago()
    {
        var capture = new LogCapture();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            productionRateLimits: true,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("one");

        for (var i = 0; i < 6; i++)
        {
            await ApiSessions.RequestCodeAsync(client, email, from: "10.31.0.1"); // the address is spent
        }

        for (var i = 0; i < 11; i++)
        {
            await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("many"), from: "10.31.0.2"); // the client is
        }

        var scopes = capture.Of("RateLimited").Select(x => x.Message.Contains("send/address") ? "address" : "client");
        Assert.Equal(["address", "client"], scopes);
    }

    [Fact]
    public async Task Bots_that_fill_the_hidden_field_and_addresses_that_may_not_register_are_logged_once_a_minute_too()
    {
        var capture = new LogCapture();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            environment: "Staging",
            time: time,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());

        async Task StopTenAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                await ApiSessions.PostAsync(
                    client,
                    "/api/registrations",
                    "registrations",
                    new
                    {
                        email = UserSeed.NewEmail("bot"),
                        givenName = "Ana",
                        surname = "Petrova",
                        countryId = country.ToString(),
                        website = "https://spam.example",
                    }
                );
                await ApiSessions.PostAsync(
                    client,
                    "/api/registrations",
                    "registrations",
                    Registration(UserSeed.NewEmail("stranger"), country)
                );
            }
        }

        await StopTenAsync();
        var firstMinute = (capture.Of("HoneypotFilled").Count, capture.Of("RegistrationClosed").Count);
        time.Advance(TimeSpan.FromSeconds(61));
        await StopTenAsync();

        Assert.Equal((1, 1), firstMinute);
        Assert.Equal(2, capture.Of("HoneypotFilled").Count);
        Assert.Equal(2, capture.Of("RegistrationClosed").Count);
        Assert.Contains("9 dropped since the last report", capture.Of("HoneypotFilled")[1].Message);
        Assert.Contains("9 refused since the last report", capture.Of("RegistrationClosed")[1].Message);
    }

    [Fact]
    public async Task A_registration_that_the_hidden_field_or_the_allow_list_stops_is_logged_without_the_address()
    {
        var capture = new LogCapture();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            environment: "Staging",
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var bot = UserSeed.NewEmail("bot");
        var stranger = UserSeed.NewEmail("stranger");

        await ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email = bot,
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
                website = "https://spam.example",
            }
        );
        await ApiSessions.PostAsync(client, "/api/registrations", "registrations", Registration(stranger, country));

        Assert.Single(capture.Of("HoneypotFilled"));
        Assert.Single(capture.Of("RegistrationClosed"));
        AssertNoneSays(capture, bot, stranger);
    }

    [Fact]
    public async Task A_code_the_provider_will_not_take_is_logged_without_the_code_or_the_address_and_answered_like_any_other()
    {
        var capture = new LogCapture();
        await using var api = new ApiFactory(
            _mongo.ConnectionString,
            configureServices: services =>
            {
                services.AddSingleton<ILoggerProvider>(capture);
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(
                    new RefusingSender("Brevo refused the message with 400 invalid_parameter: the sender is not valid.")
                );
            }
        );
        using var client = ApiClients.Of(api);
        var existing = UserSeed.NewEmail("existing");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, existing);
        var unknown = UserSeed.NewEmail("unknown");

        var forExisting = await ApiSessions.RequestCodeAsync(client, existing);
        var forUnknown = await ApiSessions.RequestCodeAsync(client, unknown);

        Assert.Equal(HttpStatusCode.Accepted, forExisting.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, forUnknown.StatusCode);
        Assert.Equal(await forExisting.Content.ReadAsStringAsync(), await forUnknown.Content.ReadAsStringAsync());
        var failed = Assert.Single(capture.Of("CodeDeliveryFailed")); // an address with no account is sent nothing
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.Contains("400 invalid_parameter", failed.Message);
        Assert.Empty(capture.Of("CodeRequested"));
        AssertNoneSays(capture, existing, unknown);
    }

    ApiFactory NewApi(LogCapture capture, TimeProvider? time = null)
    {
        return new ApiFactory(
            _mongo.ConnectionString,
            time: time,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
    }

    static object Registration(string email, Guid country)
    {
        return new
        {
            email,
            givenName = "Ana",
            surname = "Petrova",
            countryId = country.ToString(),
        };
    }

    static async Task RotateTheSecurityStampAsync(ApiFactory api, Guid userId)
    {
        using var scope = api.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NIdentityUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.True((await users.UpdateSecurityStampAsync(user!)).Succeeded);
    }

    /// <summary>Nothing a host logged, an exception included, says any of the secrets.</summary>
    static void AssertNoneSays(LogCapture capture, params string[] secrets)
    {
        foreach (var entry in capture.Entries)
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, entry.Text);
            }
        }
    }

    /// <summary>A provider that refuses every mail, the way Brevo does for a sender it does not know.</summary>
    sealed class RefusingSender : IEmailSender
    {
        readonly string _reason;

        public RefusingSender(string reason)
        {
            _reason = reason;
        }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new EmailDeliveryException(_reason);
        }
    }
}
