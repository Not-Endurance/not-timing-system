using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What changes who may do what is logged (#643, ADR-0012): an Event made or deleted, its Main Operator assigned or handed
/// over, a grant made or removed, the invitations that attached to an account, the rules of a Tenant edited. Each is an event with an id and
/// a name that stay as they are, carries the user who did it and the ids of what it was done to, and carries no email, no
/// name and nothing an account keeps: the log is the one place that is read by people who are not meant to read those.
/// </summary>
public sealed class TenancyEventLogTests : IClassFixture<MongoFixture>
{
    const int FIRST_TENANCY_EVENT = 1101;
    const int LAST_TENANCY_EVENT = 1109;

    readonly MongoFixture _mongo;

    public TenancyEventLogTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Every_change_of_authority_is_logged_with_who_did_it_and_what_it_was_done_to_and_nothing_personal()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            tenant,
            TenantRootOf(tenant),
            name: "Rositsa Rootova"
        );
        var colleague = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Kolyo Kolegov");
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, name: "Vasil Vasilev");
        var invited = UserSeed.NewEmail("invited");

        var created = await root.Page.WriteAsync(
            HttpMethod.Post,
            "/api/configure-events",
            "configure-events",
            new { name = "Logged Ride", location = "Sofia" }
        );
        var eventId = Guid.Parse(
            (await ApiSessions.ReadJsonAsync(created)).GetProperty("data").GetProperty("id").GetString()!
        );
        await root.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{eventId}/actions/assign-main-operator",
            "main-operators",
            new { accountId = colleague.Id }
        );
        var linked = await colleague.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId,
                kind = "operator",
                email = worker.Email,
            }
        );
        await colleague.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId,
                kind = "official",
                officialRole = "Steward",
                email = invited,
            }
        );
        var grantId = (await ApiSessions.ReadJsonAsync(linked)).GetProperty("data").GetProperty("id").GetString()!;
        await colleague.Page.DeleteAsync($"/api/event-grants/{grantId}");
        await root.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/tenants/{tenant}",
            "tenants",
            new { regionalRules = new { onlyAverageLoopSpeed = true } }
        );
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, colleague.Id, DateTimeOffset.UtcNow);
        await colleague.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{live}/actions/hand-over",
            "main-operators",
            new { accountId = worker.Id }
        );
        var doomed = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, colleague.Id, "Doomed Ride");
        await root.Page.DeleteAsync($"/api/configure-events/{doomed}");
        await RegisterAsync(api, invited);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var made = Assert.Single(capture.Of("EventCreated"));
        Assert.Contains(eventId.ToString(), made.Message);
        Assert.Contains(tenant, made.Message);
        Assert.Contains(root.Id.ToString(), made.Message);
        var assigned = Assert.Single(capture.Of("MainOperatorAssigned"));
        Assert.Contains(eventId.ToString(), assigned.Message);
        Assert.Contains(colleague.Id.ToString(), assigned.Message);
        Assert.Contains(root.Id.ToString(), assigned.Message);
        var handedOver = Assert.Single(capture.Of("EventHandedOver"));
        Assert.Contains(live.ToString(), handedOver.Message);
        Assert.Contains(worker.Id.ToString(), handedOver.Message);
        Assert.Contains(colleague.Id.ToString(), handedOver.Message);
        var grants = capture.Of("GrantLinked");
        Assert.Equal(2, grants.Count);
        Assert.Contains(
            grants,
            x =>
                x.Message.Contains("Operator, role none")
                && x.Message.EndsWith("pending: False.", StringComparison.Ordinal)
        );
        Assert.Contains(
            grants,
            x =>
                x.Message.Contains("Official, role Steward")
                && x.Message.EndsWith("pending: True.", StringComparison.Ordinal)
        );
        Assert.All(grants, x => Assert.Contains(colleague.Id.ToString(), x.Message));
        var removed = Assert.Single(capture.Of("GrantRemoved"));
        Assert.Contains(grantId, removed.Message);
        Assert.Contains(colleague.Id.ToString(), removed.Message);
        var edited = Assert.Single(capture.Of("TenantRulesEdited"));
        Assert.Contains(tenant, edited.Message);
        Assert.Contains(root.Id.ToString(), edited.Message);
        var deleted = Assert.Single(capture.Of("EventDeleted"));
        Assert.Contains(doomed.ToString(), deleted.Message);
        Assert.Contains(tenant, deleted.Message);
        Assert.Contains(root.Id.ToString(), deleted.Message);
        var attached = Assert.Single(capture.Of("InvitationsAttached"));
        Assert.EndsWith(": 1.", attached.Message);
        var ofTenancy = capture
            .Entries.Where(x => x.EventId.Id is >= FIRST_TENANCY_EVENT and <= LAST_TENANCY_EVENT)
            .Select(x => x.Message)
            .ToList();
        Assert.Equal(9, ofTenancy.Count);
        foreach (
            var personal in new[] { worker.Email, invited, root.Email, colleague.Email, "Rositsa", "Kolyo", "Vasil" }
        )
        {
            Assert.DoesNotContain(ofTenancy, x => x.Contains(personal, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task What_is_refused_or_changes_nothing_is_not_logged_as_a_change()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, root.Id, DateTimeOffset.UtcNow);
        async Task<HttpResponseMessage> LinkAsync(TenancySeed.Person caller)
        {
            return await caller.Page.WriteAsync(
                HttpMethod.Post,
                "/api/event-grants",
                "event-grants",
                new
                {
                    eventId = live,
                    kind = "operator",
                    email = member.Email,
                }
            );
        }

        var refused = await member.Page.WriteAsync(
            HttpMethod.Post,
            $"/api/events/{live}/actions/hand-over",
            "main-operators",
            new { accountId = member.Id }
        );
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, root.Id);
        var refusedDelete = await member.Page.DeleteAsync($"/api/configure-events/{unstarted}");
        var startedDelete = await root.Page.DeleteAsync($"/api/configure-events/{live}");
        var refusedGrant = await LinkAsync(member);
        var first = await LinkAsync(root);
        var again = await LinkAsync(root);
        var sameRules = await root.Page.WriteAsync(
            HttpMethod.Patch,
            $"/api/tenants/{tenant}",
            "tenants",
            new { regionalRules = new { onlyAverageLoopSpeed = false } }
        );

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refusedDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, startedDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refusedGrant.StatusCode);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameRules.StatusCode);
        Assert.Empty(capture.Of("EventHandedOver"));
        Assert.Empty(capture.Of("EventDeleted"));
        Assert.Single(capture.Of("GrantLinked"));
        Assert.Empty(capture.Of("TenantRulesEdited"));
    }

    [Fact]
    public async Task A_pending_grant_that_is_linked_again_once_the_person_has_an_account_is_logged_as_the_change_it_is()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var worker = await AccountAsync(_mongo.ConnectionString, tenant);
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, live, "Operator", null, worker.Email, null);

        var response = await mainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId = live,
                kind = "operator",
                email = worker.Email,
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logged = Assert.Single(capture.Of("GrantLinked"));
        Assert.EndsWith("pending: False.", logged.Message);
    }

    [Fact]
    public async Task A_grant_that_waits_with_a_null_account_is_taken_on_signing_in_like_one_that_has_no_such_field()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var worker = await AccountAsync(_mongo.ConnectionString, tenant);
        var live = Guid.NewGuid();
        var missing = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Operator",
            null,
            worker.Email,
            null
        );
        var nulled = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Official",
            "Steward",
            worker.Email,
            null
        );
        await EventSeed
            .Grants(_mongo.ConnectionString)
            .UpdateOneAsync(
                new BsonDocument("_id", EventSeed.Binary(nulled)),
                Builders<BsonDocument>.Update.Set("AccountId", BsonNull.Value)
            );

        await ApiSessions.SignInAsync(api, client, worker.Email);

        foreach (var id in new[] { missing, nulled })
        {
            var stored = await EventSeed
                .Grants(_mongo.ConnectionString)
                .Find(new BsonDocument("_id", EventSeed.Binary(id)))
                .FirstAsync();
            Assert.Equal(worker.Id, stored["AccountId"].AsGuid);
        }

        Assert.EndsWith(": 2.", Assert.Single(capture.Of("InvitationsAttached")).Message);
    }

    [Fact]
    public async Task A_sign_in_goes_on_when_the_pending_grants_waiting_for_the_address_cannot_be_attached()
    {
        var capture = new LogCapture();
        await using var api = NewApi(capture);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var worker = await AccountAsync(_mongo.ConnectionString, tenant);
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(UserSeed.DATABASE);

        // The host has started and made its indexes. From here the grants cannot be written, as if the store did not
        // answer: a view of another collection takes their place, and a view cannot be updated.
        await database.DropCollectionAsync("event_grants");
        await database.CreateViewAsync("event_grants", "users", new EmptyPipelineDefinition<BsonDocument>());
        try
        {
            var cookie = await ApiSessions.SignInAsync(api, client, worker.Email);

            var me = await ApiSessions.GetAsync(client, "/api/me", cookie);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            var failed = Assert.Single(capture.Of("InvitationsNotAttached"));
            Assert.Equal(LogLevel.Error, failed.Level);
            Assert.NotNull(failed.Exception);
            Assert.Contains(worker.Id.ToString(), failed.Message);
            Assert.DoesNotContain(worker.Email, failed.Message);
        }
        finally
        {
            await database.DropCollectionAsync("event_grants");
        }
    }

    ApiFactory NewApi(LogCapture capture)
    {
        return new ApiFactory(
            _mongo.ConnectionString,
            configureServices: services => services.AddSingleton<ILoggerProvider>(capture)
        );
    }

    /// <summary>A person registers with the address, as a visitor does: the details, then the code that was mailed.</summary>
    async Task RegisterAsync(ApiFactory api, string email)
    {
        using var browser = ApiClients.OfBrowser(api);
        var iso = CountrySeed.UniqueIsoCode();
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Land " + iso, iso);
        await ApiSessions.PostAsync(
            browser,
            "/api/registrations",
            "registrations",
            new
            {
                email,
                givenName = "Ana",
                surname = "Petrova",
                countryId = country.ToString(),
            }
        );
        var created = await ApiSessions.CreateSessionAsync(browser, email, ApiSessions.CodeSentTo(api, email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
}
