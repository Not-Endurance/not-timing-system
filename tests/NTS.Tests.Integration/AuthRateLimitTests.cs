using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Not.Identity.Email;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// Asking for a code and trying one are limited (#601, ADR-0002): per address, per client and overall, with the limits
/// of production (5, 10 and 100 an hour). The answer of a refusal is the same for an address that has an account and
/// one that has none, and a refused request does nothing. A person who types a wrong code can be locked out by nobody
/// else: the failures of a client count against that client.
/// </summary>
public sealed class AuthRateLimitTests : IClassFixture<MongoFixture>
{
    const string ONE_HOUR = "01:00:00";

    readonly MongoFixture _mongo;

    public AuthRateLimitTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_address_is_sent_five_codes_an_hour_and_the_sixth_request_is_refused_whether_or_not_it_has_an_account()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var existing = await SeedUser();
        var unknown = UserSeed.NewEmail("unknown");

        var forExisting = await RequestCodesAsync(client, existing, 6, from: "10.1.0.1");
        var forUnknown = await RequestCodesAsync(client, unknown, 6, from: "10.1.0.2");

        Assert.All(forExisting.Take(5), x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.All(forUnknown.Take(5), x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        var refusedExisting = forExisting[5];
        var refusedUnknown = forUnknown[5];
        Assert.Equal(HttpStatusCode.TooManyRequests, refusedExisting.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refusedUnknown.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(refusedExisting));
        Assert.Equal(
            await refusedExisting.Content.ReadAsStringAsync(),
            await refusedUnknown.Content.ReadAsStringAsync()
        );
        Assert.Equal(refusedExisting.Content.Headers.ContentType, refusedUnknown.Content.Headers.ContentType);
        Assert.Equal(refusedExisting.Headers.RetryAfter?.Delta, refusedUnknown.Headers.RetryAfter?.Delta);
        Assert.InRange(
            refusedExisting.Headers.RetryAfter!.Delta!.Value,
            TimeSpan.FromMinutes(59),
            TimeSpan.Parse(ONE_HOUR)
        );
    }

    [Fact]
    public async Task The_budget_of_an_address_is_back_when_the_hour_is_over()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("again");
        var spent = await RequestCodesAsync(client, email, 6, from: "10.1.1.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, spent[5].StatusCode);

        time.Advance(TimeSpan.FromMinutes(30));
        var stillRefused = await ApiSessions.RequestCodeAsync(client, email, from: "10.1.1.1");
        time.Advance(TimeSpan.FromMinutes(31));
        var allowedAgain = await ApiSessions.RequestCodeAsync(client, email, from: "10.1.1.1");

        Assert.Equal(HttpStatusCode.TooManyRequests, stillRefused.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, allowedAgain.StatusCode);
    }

    [Fact]
    public async Task A_client_is_allowed_ten_requests_an_hour_whatever_the_addresses_and_another_client_is_not_affected()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        var tenAddresses = new List<HttpResponseMessage>();
        for (var i = 0; i < 11; i++)
        {
            tenAddresses.Add(await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("many"), from: "10.2.0.1"));
        }

        var another = await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("other"), from: "10.2.0.2");

        Assert.All(tenAddresses.Take(10), x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, tenAddresses[10].StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(tenAddresses[10]));
        Assert.Equal(HttpStatusCode.Accepted, another.StatusCode);
    }

    [Fact]
    public async Task A_person_on_IPv6_has_one_budget_for_their_whole_network_and_a_mapped_IPv4_address_counts_as_IPv4()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        var network = new List<HttpResponseMessage>();
        for (var i = 1; i <= 11; i++)
        {
            // Another address of the same /64 for every request: the way to look like many clients is no way at all.
            network.Add(
                await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("v6"), from: $"2001:db8:aa:1:{i:x}::{i:x}")
            );
        }

        var anotherNetwork = await ApiSessions.RequestCodeAsync(
            client,
            UserSeed.NewEmail("v6"),
            from: "2001:db8:aa:2::1"
        );
        var mapped = new List<HttpResponseMessage>();
        for (var i = 0; i < 6; i++)
        {
            mapped.Add(await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("v4"), from: "10.11.0.1"));
            mapped.Add(await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("v4"), from: "::ffff:10.11.0.1"));
        }

        Assert.All(network.Take(10), x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, network[10].StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, anotherNetwork.StatusCode);
        Assert.All(mapped.Take(10), x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.All(mapped.Skip(10), x => Assert.Equal(HttpStatusCode.TooManyRequests, x.StatusCode));
    }

    [Fact]
    public async Task The_platform_is_sent_a_hundred_codes_an_hour_and_the_hundred_and_first_request_is_refused()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        for (var i = 0; i < 100; i++)
        {
            var sent = await ApiSessions.RequestCodeAsync(
                client,
                UserSeed.NewEmail("all"),
                from: $"10.3.{i / 250}.{i % 250 + 1}"
            );
            Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        }

        var refused = await ApiSessions.RequestCodeAsync(client, UserSeed.NewEmail("one-more"), from: "10.3.9.9");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(refused));
    }

    [Fact]
    public async Task Registering_and_asking_for_a_code_draw_on_the_same_budget_and_a_refusal_sends_and_stores_nothing()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Bulgaria", CountrySeed.UniqueIsoCode());
        var email = UserSeed.NewEmail("both");
        const string from = "10.4.0.1";

        var answers = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            answers.Add(await RegisterAsync(client, email, country, from));
        }

        for (var i = 0; i < 2; i++)
        {
            answers.Add(await ApiSessions.RequestCodeAsync(client, email, from: from));
        }

        var mailsBefore = Outbox(api).Messages.Count;
        time.Advance(TimeSpan.FromMinutes(2)); // past the cooldown of a code: only the limit stands in the way
        var registration = await RegisterAsync(client, email, country, from);
        var signIn = await ApiSessions.RequestCodeAsync(client, email, from: from);

        Assert.All(answers, x => Assert.Equal(HttpStatusCode.Accepted, x.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, registration.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, signIn.StatusCode);
        Assert.Equal(mailsBefore, Outbox(api).Messages.Count);
    }

    [Fact]
    public async Task A_request_that_is_not_valid_is_refused_as_it_was_and_does_not_use_the_budget()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);
        var email = UserSeed.NewEmail("valid");

        for (var i = 0; i < 6; i++)
        {
            var malformed = await ApiSessions.RequestCodeAsync(client, "not-an-address", from: "10.5.0.1");
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        }

        var valid = await ApiSessions.RequestCodeAsync(client, email, from: "10.5.0.1");

        Assert.Equal(HttpStatusCode.Accepted, valid.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_codes_from_a_client_for_an_address_lock_that_client_out_of_it_and_nobody_else()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email, from: "10.6.0.1");
        var code = ApiSessions.CodeSentTo(api, email);
        var wrong = code == "000000" ? "000001" : "000000";

        var guesses = new List<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
        {
            guesses.Add(await ApiSessions.CreateSessionAsync(client, email, wrong, from: "10.6.0.9"));
        }

        // The attacker's challenge is spent, and the person asks for a new code once the cooldown is over.
        time.Advance(TimeSpan.FromMinutes(2));
        await ApiSessions.RequestCodeAsync(client, email, from: "10.6.0.1");
        var fresh = ApiSessions.CodeSentTo(api, email);
        var rightCodeFromTheLockedClient = await ApiSessions.CreateSessionAsync(client, email, fresh, from: "10.6.0.9");
        var rightCodeFromThePerson = await ApiSessions.CreateSessionAsync(client, email, fresh, from: "10.6.0.1");

        Assert.All(guesses, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, rightCodeFromTheLockedClient.StatusCode);
        Assert.Equal("rate-limited", await ErrorCodeAsync(rightCodeFromTheLockedClient));
        Assert.NotNull(rightCodeFromTheLockedClient.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.Created, rightCodeFromThePerson.StatusCode);
    }

    [Fact]
    public async Task A_client_may_get_ten_codes_wrong_an_hour_whatever_the_addresses_and_the_eleventh_attempt_is_refused()
    {
        await using var api = NewApi(out _);
        using var client = ApiClients.Of(api);

        var attempts = new List<HttpResponseMessage>();
        for (var i = 0; i < 11; i++)
        {
            attempts.Add(
                await ApiSessions.CreateSessionAsync(client, UserSeed.NewEmail("guess"), "123456", from: "10.7.0.1")
            );
        }

        var another = await ApiSessions.CreateSessionAsync(
            client,
            UserSeed.NewEmail("guess"),
            "123456",
            from: "10.7.0.2"
        );

        Assert.All(attempts.Take(10), x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, attempts[10].StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, another.StatusCode);
    }

    [Fact]
    public async Task A_hundred_wrong_codes_an_hour_in_all_stop_the_attempts_of_everyone_until_the_hour_is_over()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var email = await SeedUser();
        await ApiSessions.RequestCodeAsync(client, email, from: "10.8.9.1");
        var code = ApiSessions.CodeSentTo(api, email);

        for (var i = 0; i < 100; i++)
        {
            var wrong = await ApiSessions.CreateSessionAsync(
                client,
                UserSeed.NewEmail("guess"),
                "123456",
                from: $"10.8.{i / 250}.{i % 250 + 1}"
            );
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var refused = await ApiSessions.CreateSessionAsync(client, email, code, from: "10.8.9.1");
        time.Advance(TimeSpan.FromMinutes(61));
        await ApiSessions.RequestCodeAsync(client, email, from: "10.8.9.1");
        var afterTheHour = await ApiSessions.CreateSessionAsync(
            client,
            email,
            ApiSessions.CodeSentTo(api, email),
            from: "10.8.9.1"
        );

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterTheHour.StatusCode);
    }

    [Fact]
    public async Task A_right_code_does_not_use_up_the_budget_of_wrong_ones()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var email = await SeedUser();

        // Four rounds of one mistake and one right code. Were the right codes counted too, the mistake of the third
        // round would be the fifth attempt, and the right code after it would be refused.
        var statuses = new List<HttpStatusCode>();
        for (var round = 0; round < 4; round++)
        {
            await ApiSessions.RequestCodeAsync(client, email, from: "10.9.0.1");
            var code = ApiSessions.CodeSentTo(api, email);
            var wrong = code == "000000" ? "000001" : "000000";
            statuses.Add((await ApiSessions.CreateSessionAsync(client, email, wrong, from: "10.9.0.1")).StatusCode);
            statuses.Add((await ApiSessions.CreateSessionAsync(client, email, code, from: "10.9.0.1")).StatusCode);
            time.Advance(TimeSpan.FromMinutes(2));
        }

        Assert.Equal(
            [
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Created,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Created,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Created,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Created,
            ],
            statuses
        );
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        return new ApiFactory(_mongo.ConnectionString, time: time, productionRateLimits: true);
    }

    static async Task<List<HttpResponseMessage>> RequestCodesAsync(
        HttpClient client,
        string email,
        int count,
        string from
    )
    {
        var answers = new List<HttpResponseMessage>();
        for (var i = 0; i < count; i++)
        {
            answers.Add(await ApiSessions.RequestCodeAsync(client, email, from: from));
        }

        return answers;
    }

    static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, Guid country, string from)
    {
        return ApiSessions.PostAsync(
            client,
            "/api/registrations",
            "registrations",
            new
            {
                email,
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
            },
            from: from
        );
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    static IEmailOutbox Outbox(ApiFactory api)
    {
        return api.Services.GetRequiredService<IEmailOutbox>();
    }

    async Task<string> SeedUser()
    {
        var email = UserSeed.NewEmail();
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        return email;
    }
}
