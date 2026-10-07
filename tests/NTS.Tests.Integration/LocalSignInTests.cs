#if DEBUG
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.LocalSignInHarness;

namespace NTS.Tests.Integration;

/// <summary>
/// The local sign in as (#607): a developer who runs the platform on their machine signs in as an account on an allow-list
/// without a code. It is in a Debug build only (<see cref="LocalSignInBuildTests"/> asserts the type is not in a Release one,
/// and a Release host has no route even when it is told to), only in Development, only when a setting names the emails, only
/// for a request from the machine itself, and never on a database that is marked Production or is not marked at all. It
/// confirms nothing of the address and takes no invitation. The real host over a MongoDB in a container, the address of the
/// caller named by the test.
/// </summary>
public sealed class LocalSignInTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public LocalSignInTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_account_on_the_allow_list_is_signed_in_as_by_a_request_from_the_machine_itself()
    {
        var email = UserSeed.NewEmail("local");
        var id = await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await MarkAsync("Staging");
        await using var api = ApiOf(email);
        using var client = api.CreateClient();

        var response = await SignInAsAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("dev", attributes.GetProperty("method").GetString());
        Assert.Equal(id.ToString(), attributes.GetProperty("accountId").GetString());
        var cookie = SessionCookie.From(response);
        Assert.NotNull(cookie);
        var me = await ApiSessions.GetAsync(client, "/api/me", cookie);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(
            email,
            (await ApiSessions.ReadJsonAsync(me))
                .GetProperty("data")
                .GetProperty("attributes")
                .GetProperty("email")
                .GetString()
        );
    }

    [Fact]
    public async Task The_address_may_be_typed_in_any_case_and_with_spaces_and_the_account_keeps_its_address_unconfirmed()
    {
        var email = UserSeed.NewEmail("local");
        var id = await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await MarkAsync("Development");
        await using var api = ApiOf($" {email.ToUpperInvariant()} ");
        using var client = api.CreateClient();

        var response = await SignInAsAsync(client, $"  {email.ToUpperInvariant()}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = await UserSeed
            .Users(_mongo.ConnectionString)
            .Find(new BsonDocument("_id", LegacyTenantData.Uuid(id)))
            .SingleAsync();
        Assert.False(stored.GetValue("EmailConfirmed", false).AsBoolean); // nothing was proved of the address
        Assert.False(string.IsNullOrEmpty(stored["SecurityStamp"].AsString)); // a session needs a stamp
    }

    [Fact]
    public async Task An_invitation_waiting_for_the_address_is_not_taken_by_signing_in_as_somebody()
    {
        var email = UserSeed.NewEmail("local");
        var id = await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await MarkAsync("Staging");
        var invitation = Guid.NewGuid();
        await EventSeed
            .Grants(_mongo.ConnectionString)
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", EventSeed.Binary(invitation) },
                    { "TenantId", "nts" },
                    { "EventId", EventSeed.Binary(Guid.NewGuid()) },
                    { "Kind", "Operator" },
                    { "Email", email },
                }
            );
        await using var api = ApiOf(email);
        using var client = api.CreateClient();

        var response = await SignInAsAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var grant = await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(new BsonDocument("_id", EventSeed.Binary(invitation)))
            .SingleAsync();
        Assert.False(grant.Contains("AccountId"));
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Without_an_allow_list_there_is_no_route_and_no_page()
    {
        await MarkAsync("Staging");
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = api.CreateClient();

        var post = await SignInAsAsync(client, "someone@example.test");
        var page = await GetPageAsync(client);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.DoesNotContain("sign-in-as-page", await page.Content.ReadAsStringAsync());
        Assert.Empty(api.Services.GetServices<HostPublicEndpoints>());
    }

    [Fact]
    public async Task Outside_Development_there_is_no_route_even_with_an_allow_list()
    {
        var email = UserSeed.NewEmail("local");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await MarkAsync("Staging");
        await using var api = ApiOf(email, environment: "Staging");
        using var client = api.CreateClient();

        var post = await SignInAsAsync(client, email);
        var page = await GetPageAsync(client);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.DoesNotContain("sign-in-as-page", await page.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("10.0.0.7", null, null)] // from another machine
    [InlineData("::1", null, "forwarded")] // behind a proxy
    [InlineData(LOOPBACK, "evil.example", null)] // by a name that is not the machine's own
    [InlineData(null, null, null)] // from nowhere it can be told
    public async Task A_request_that_does_not_come_from_the_machine_to_its_own_name_directly_is_refused(
        string? remote,
        string? host,
        string? proxy
    )
    {
        var email = UserSeed.NewEmail("local");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await MarkAsync("Staging");
        await using var api = ApiOf(email);
        using var client = api.CreateClient();

        var response = await SignInAsAsync(client, email, remote, host, proxy);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("local-only", await ErrorCodeAsync(response));
        Assert.Null(SessionCookie.From(response));
    }

    [Fact]
    public async Task A_database_that_is_marked_Production_is_refused_and_one_that_is_not_marked_is_refused_too()
    {
        var email = UserSeed.NewEmail("local");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);
        await using var api = ApiOf(email);
        using var client = api.CreateClient();

        await MarkAsync(null);
        var unmarked = await SignInAsAsync(client, email);
        await MarkAsync("Production");
        var production = await SignInAsAsync(client, email);
        await MarkAsync("Staging");
        var staging = await SignInAsAsync(client, email);

        Assert.Equal(HttpStatusCode.Conflict, unmarked.StatusCode);
        Assert.Equal("environment-not-marked", await ErrorCodeAsync(unmarked));
        Assert.Equal(HttpStatusCode.Forbidden, production.StatusCode);
        Assert.Equal("production-database", await ErrorCodeAsync(production));
        Assert.Null(SessionCookie.From(production));
        Assert.Equal(HttpStatusCode.Created, staging.StatusCode); // the marker is looked at on every request
    }

    [Fact]
    public async Task An_account_that_is_not_on_the_allow_list_is_refused_and_one_that_is_not_there_is_not_found()
    {
        var listed = UserSeed.NewEmail("listed");
        var other = UserSeed.NewEmail("other");
        var missing = UserSeed.NewEmail("missing");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, other);
        await MarkAsync("Staging");
        await using var api = ApiOf(listed, missing);
        using var client = api.CreateClient();

        var notListed = await SignInAsAsync(client, other);
        var notThere = await SignInAsAsync(client, missing);
        var nothing = await PostAsync(client, "{ \"data\": { \"type\": \"sessions\", \"attributes\": {} } }");

        Assert.Equal(HttpStatusCode.Forbidden, notListed.StatusCode);
        Assert.Equal("not-on-the-allow-list", await ErrorCodeAsync(notListed));
        Assert.Equal(HttpStatusCode.NotFound, notThere.StatusCode);
        Assert.Equal("account-not-found", await ErrorCodeAsync(notThere));
        Assert.Equal(HttpStatusCode.Forbidden, nothing.StatusCode);
    }

    [Fact]
    public async Task The_page_lists_the_emails_of_the_allow_list_to_the_machine_and_to_nobody_else()
    {
        var email = UserSeed.NewEmail("local");
        await using var api = ApiOf(email);
        using var client = api.CreateClient();

        var local = await GetPageAsync(client, LOOPBACK);
        var remote = await GetPageAsync(client, "10.0.0.7");

        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        var html = await local.Content.ReadAsStringAsync();
        Assert.Contains("sign-in-as-page", html);
        Assert.Contains(email, html);
        Assert.Equal(HttpStatusCode.Forbidden, remote.StatusCode);
        Assert.DoesNotContain(email, await remote.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_public_endpoints_of_a_host_with_the_route_are_the_list_of_the_Api_and_the_two_of_the_route()
    {
        var email = UserSeed.NewEmail("local");
        await using var api = ApiOf(email);

        var entries = api.Services.GetRequiredService<HostPublicEndpoints>().Entries;

        Assert.Equal(
            ["sign-in GET,HEAD dev/sign-in-as", "sign-in POST api/dev/sessions"],
            entries.Select(x => $"sign-in {string.Join(",", x.Methods)} {x.Pattern}").Order(StringComparer.Ordinal)
        );
        Assert.Equal(EndpointAccess.SignIn, PublicEndpoints.AccessOf("POST", "api/dev/sessions", entries));
        Assert.Equal(EndpointAccess.Protected, PublicEndpoints.AccessOf("POST", "api/dev/sessions")); // the list of the Api does not name it
    }

    ApiFactory ApiOf(string email, string? second = null, string environment = "Development")
    {
        return ApiOver(_mongo.ConnectionString, email, second, environment);
    }

    Task MarkAsync(string? name)
    {
        return SetMarkerAsync(_mongo.ConnectionString, name);
    }
}
#endif
