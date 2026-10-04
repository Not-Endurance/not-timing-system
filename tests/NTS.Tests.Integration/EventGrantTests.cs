using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using NoTiming.Api.Features.Events;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The Main Operator links people to the Officials and Operators of an Event by their exact email (ADR-0012, #643). An
/// email that has an account gives that account the grant at once; one that has none is a pending invitation that
/// attaches to the account that registers with it, and gives nothing until then. A grant is the Event's and stays in the
/// Event's Tenant, ends the moment it is removed, and names a person by an email that is never given back unmasked.
/// </summary>
public sealed class EventGrantTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public EventGrantTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_Main_Operator_links_an_account_to_the_Event_as_an_Operator_by_its_exact_email()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant, name: "Georgi Dimitrov");

        var response = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var resource = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data");
        Assert.Equal("event-grants", resource.GetProperty("type").GetString());
        var attributes = resource.GetProperty("attributes");
        Assert.Equal(setup.Live.ToString(), attributes.GetProperty("eventId").GetString());
        Assert.Equal("operator", attributes.GetProperty("kind").GetString());
        Assert.False(attributes.GetProperty("pending").GetBoolean());
        Assert.Equal(worker.Id.ToString(), attributes.GetProperty("accountId").GetString());
        Assert.Equal("Georgi Dimitrov", attributes.GetProperty("displayName").GetString());
        Assert.False(attributes.TryGetProperty("officialRole", out _));
        Assert.DoesNotContain(worker.Email, attributes.ToString());
        var stored = (await GrantOfAsync(Guid.Parse(resource.GetProperty("id").GetString()!)))!;
        Assert.Equal(setup.Tenant, stored["TenantId"].AsString);
        Assert.Equal(worker.Email, stored["Email"].AsString);
        Assert.Equal(worker.Id, stored["AccountId"].AsGuid);
        Assert.True((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());
    }

    [Theory]
    [InlineData("Steward", true)]
    [InlineData("ChiefSteward", true)]
    [InlineData("GroundJury", true)]
    [InlineData("GroundJuryPresident", true)]
    [InlineData("VeterinaryCommissionMember", false)]
    [InlineData("TechnicalDelegate", false)]
    public async Task An_account_linked_as_an_Official_may_send_a_Snapshot_only_in_the_roles_that_may(
        string role,
        bool canSnapshot
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var response = await LinkOfficialAsync(setup.MainOperator, setup.Live, role, email: official.Email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.Equal("official", attributes.GetProperty("kind").GetString());
        Assert.Equal(role, attributes.GetProperty("officialRole").GetString());
        Assert.Equal(
            canSnapshot,
            (await CapabilitiesAsync(official, setup.Live)).GetProperty("canSnapshot").GetBoolean()
        );
    }

    [Fact]
    public async Task An_account_can_be_linked_by_its_id_which_is_how_a_person_found_by_name_is_linked()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var response = await LinkOperatorAsync(setup.MainOperator, setup.Live, accountId: worker.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(worker.Email, (await GrantOfAsync(await GrantIdAsync(response)))!["Email"].AsString);
        Assert.True((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());
    }

    [Fact]
    public async Task An_account_of_another_Tenant_can_be_linked_because_accounts_are_global_and_access_is_a_grant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var abroad = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var visitor = await SignedInAsync(api, client, _mongo.ConnectionString, abroad);

        var response = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: visitor.Email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await CapabilitiesAsync(visitor, setup.Live)).GetProperty("canSnapshot").GetBoolean());
        Assert.Equal(setup.Tenant, (await GrantOfAsync(await GrantIdAsync(response)))!["TenantId"].AsString); // the grant is the Event's Tenant's
    }

    [Fact]
    public async Task An_email_that_has_no_account_is_a_pending_invitation_that_gives_nothing_and_names_nobody()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var email = UserSeed.NewEmail("invited");

        var response = await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attributes = (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
        Assert.True(attributes.GetProperty("pending").GetBoolean());
        Assert.False(attributes.TryGetProperty("accountId", out _));
        Assert.False(attributes.TryGetProperty("displayName", out _));
        Assert.DoesNotContain(email, attributes.ToString());
        var stored = (await GrantsOfEventAsync(setup.Live)).Single();
        Assert.False(stored.Contains("AccountId"));
        Assert.Equal(email, stored["Email"].AsString);
    }

    [Fact]
    public async Task Registering_with_the_email_of_an_invitation_attaches_it_and_the_grant_then_counts()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var email = UserSeed.NewEmail("invited");
        await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: email);
        var otherEvent = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        await LinkOperatorAsync(setup.MainOperator, otherEvent, email: email);

        var registered = await RegisterAsync(api, email);

        var grants = (await GrantsOfEventAsync(setup.Live)).Concat(await GrantsOfEventAsync(otherEvent)).ToList();
        Assert.Equal(2, grants.Count);
        Assert.All(grants, grant => Assert.Equal(registered.Id, grant["AccountId"].AsGuid));
        Assert.True((await CapabilitiesAsync(registered, setup.Live)).GetProperty("canSnapshot").GetBoolean());
        Assert.True((await CapabilitiesAsync(registered, otherEvent)).GetProperty("canSnapshot").GetBoolean());
    }

    [Fact]
    public async Task An_invitation_that_waits_for_an_account_that_already_exists_attaches_at_the_next_sign_in_with_a_code()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await AccountAsync(_mongo.ConnectionString, setup.Tenant);
        var id = Guid.NewGuid();
        await EventSeed
            .Grants(_mongo.ConnectionString)
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", EventSeed.Binary(id) },
                    { "TenantId", setup.Tenant },
                    { "EventId", EventSeed.Binary(setup.Live) },
                    { "Kind", "Operator" },
                    { "Email", worker.Email },
                }
            );
        var page = new PageClient(client);

        page.Set(await ApiSessions.SignInAsync(api, client, worker.Email));

        Assert.Equal(worker.Id, (await GrantOfAsync(id))!["AccountId"].AsGuid);
        Assert.True(
            (await CapabilitiesAsync(new Person(worker.Id, worker.Email, page), setup.Live))
                .GetProperty("canSnapshot")
                .GetBoolean()
        );
    }

    [Fact]
    public async Task Linking_an_email_that_has_an_account_now_attaches_the_invitation_that_waited_for_it()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var waiting = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.Live,
            "Operator",
            null,
            worker.Email,
            null
        );

        var response = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(waiting, await GrantIdAsync(response));
        Assert.Equal(worker.Id, (await GrantOfAsync(waiting))!["AccountId"].AsGuid);
        Assert.Single(await GrantsOfEventAsync(setup.Live));
    }

    [Fact]
    public async Task Registering_attaches_only_the_invitations_to_the_address_that_registered()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var registering = UserSeed.NewEmail("invited");
        var staying = UserSeed.NewEmail("invited");
        await LinkOperatorAsync(setup.MainOperator, setup.Live, email: registering);
        await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: staying);

        var registered = await RegisterAsync(api, registering);

        var grants = await GrantsOfEventAsync(setup.Live);
        Assert.Equal(registered.Id, grants.Single(x => x["Email"].AsString == registering)["AccountId"].AsGuid);
        Assert.False(grants.Single(x => x["Email"].AsString == staying).Contains("AccountId"));
    }

    [Fact]
    public async Task The_database_itself_refuses_the_same_person_in_the_same_place_twice()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client); // the host has started, and made its indexes
        var email = UserSeed.NewEmail("twice");
        await EventSeed.GrantAsync(_mongo.ConnectionString, setup.Tenant, setup.Live, "Operator", null, email, null);

        await Assert.ThrowsAsync<MongoWriteException>(
            () => EventSeed.GrantAsync(_mongo.ConnectionString, setup.Tenant, setup.Live, "Operator", null, email, null)
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.Live,
            "Official",
            "Steward",
            email,
            null
        );
        await Assert.ThrowsAsync<MongoWriteException>(
            () =>
                EventSeed.GrantAsync(
                    _mongo.ConnectionString,
                    setup.Tenant,
                    setup.Live,
                    "Official",
                    "Steward",
                    email,
                    null
                )
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.Live,
            "Official",
            "ChiefSteward",
            email,
            null
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            Guid.NewGuid(),
            "Operator",
            null,
            email,
            null
        );
    }

    [Fact]
    public async Task Linking_the_same_person_to_the_same_place_twice_gives_the_first_grant_again()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var first = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email);
        var again = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email.ToUpperInvariant());
        var asOfficial = await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: worker.Email);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await GrantIdAsync(first), await GrantIdAsync(again));
        Assert.Equal(HttpStatusCode.Created, asOfficial.StatusCode);
        Assert.Equal(2, (await GrantsOfEventAsync(setup.Live)).Count);
    }

    [Fact]
    public async Task Another_person_or_another_role_is_another_grant()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var first = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var second = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var asSteward = await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: first.Email);
        var asChiefSteward = await LinkOfficialAsync(
            setup.MainOperator,
            setup.Live,
            "ChiefSteward",
            email: first.Email
        );
        var firstAsOperator = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: first.Email);
        var secondAsOperator = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: second.Email);

        var ids = new HashSet<Guid>();
        foreach (var response in new[] { asSteward, asChiefSteward, firstAsOperator, secondAsOperator })
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add(await GrantIdAsync(response));
        }

        Assert.Equal(4, (await GrantsOfEventAsync(setup.Live)).Count);
        Assert.Equal(4, ids.Count);
    }

    [Fact]
    public async Task An_Event_holds_a_limited_number_of_grants_and_one_it_has_is_still_answered_when_it_is_full()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var otherEvent = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var limit = EventGrantStore.MAX_GRANTS_PER_EVENT;
        await EventSeed
            .Grants(_mongo.ConnectionString)
            .InsertManyAsync(
                Enumerable
                    .Range(0, limit - 1)
                    .Select(x => new BsonDocument
                    {
                        { "_id", EventSeed.Binary(Guid.NewGuid()) },
                        { "TenantId", setup.Tenant },
                        { "EventId", EventSeed.Binary(setup.Live) },
                        { "Kind", "Operator" },
                        { "Email", $"waiting-{x}@example.test" },
                    })
            );

        var last = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: "the-last@example.test");
        var over = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: "one-more@example.test");
        var again = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: "the-last@example.test");
        var elsewhere = await LinkOperatorAsync(setup.MainOperator, otherEvent, email: "one-more@example.test");

        Assert.Equal(HttpStatusCode.Created, last.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, over.StatusCode);
        Assert.Equal("grant-limit-reached", await ErrorCodeAsync(over));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await GrantIdAsync(last), await GrantIdAsync(again));
        Assert.Equal(HttpStatusCode.Created, elsewhere.StatusCode);
        Assert.Equal(limit, (await GrantsOfEventAsync(setup.Live)).Count);
        Assert.DoesNotContain(
            await GrantsOfEventAsync(setup.Live),
            x => x["Email"].AsString == "one-more@example.test"
        );
    }

    [Fact]
    public async Task The_grants_of_an_Event_that_has_ended_are_still_listed_to_its_Main_Operator_and_none_is_added_or_removed()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        var grant = await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            historic,
            "Operator",
            null,
            worker.Email,
            worker.Id
        );

        var listed = await setup.MainOperator.Page.GetAsync($"/api/event-grants?filter=eventId eq {historic}");
        var removed = await setup.MainOperator.Page.DeleteAsync($"/api/event-grants/{grant}");
        var strangers = await setup.Member.Page.GetAsync($"/api/event-grants?filter=eventId eq {historic}");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Single((await ApiSessions.ReadJsonAsync(listed)).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(removed));
        Assert.NotNull(await GrantOfAsync(grant));
        Assert.Equal(HttpStatusCode.Forbidden, strangers.StatusCode);
    }

    [Fact]
    public async Task Only_the_Main_Operator_of_the_Event_links_and_the_Developer_only_before_it_starts()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperatorElsewhere = await SignedInAsync(api, client, _mongo.ConnectionString, other);
        await EventSeed.LiveAsync(_mongo.ConnectionString, other, mainOperatorElsewhere.Id, DateTimeOffset.UtcNow);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, setup.Tenant, setup.MainOperator.Id);

        foreach (var caller in new[] { setup.Root, setup.Member, setup.Developer, mainOperatorElsewhere })
        {
            var response = await LinkOperatorAsync(caller, setup.Live, accountId: worker.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("not-main-operator", await ErrorCodeAsync(response));
        }

        var developerBefore = await LinkOperatorAsync(setup.Developer, unstarted, accountId: worker.Id);
        Assert.Equal(HttpStatusCode.Created, developerBefore.StatusCode);
        Assert.Empty(await GrantsOfEventAsync(setup.Live));
    }

    [Fact]
    public async Task Nothing_is_linked_to_an_Event_that_has_ended_or_to_one_that_is_not_there()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var historic = await EventSeed.HistoricAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );

        var ended = await LinkOperatorAsync(setup.MainOperator, historic, accountId: worker.Id);
        var missing = await LinkOperatorAsync(setup.MainOperator, Guid.NewGuid(), accountId: worker.Id);

        Assert.Equal(HttpStatusCode.Conflict, ended.StatusCode);
        Assert.Equal("event-ended", await ErrorCodeAsync(ended));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("tenant-root")]
    [InlineData("developer")]
    [InlineData("main-operator")]
    [InlineData("")]
    public async Task A_grant_is_of_a_kind_the_Event_has_and_never_a_role_of_the_Tenant_or_the_platform(string kind)
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var before = await UserSeed
            .Users(_mongo.ConnectionString)
            .Find(new BsonDocument("Email", worker.Email))
            .FirstAsync();

        var response = await setup.MainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId = setup.Live,
                kind,
                email = worker.Email,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid-kind", await ErrorCodeAsync(response));
        Assert.Equal(
            before,
            await UserSeed.Users(_mongo.ConnectionString).Find(new BsonDocument("Email", worker.Email)).FirstAsync()
        );
    }

    [Theory]
    [InlineData("official", null, "invalid-role")]
    [InlineData("official", "Nobody", "invalid-role")]
    [InlineData("official", "99", "invalid-role")]
    [InlineData("operator", "Steward", "invalid-role")]
    public async Task An_Official_has_a_role_of_the_domain_and_an_Operator_has_none(
        string kind,
        string? role,
        string code
    )
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var response = await setup.MainOperator.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId = setup.Live,
                kind,
                officialRole = role,
                email = worker.Email,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_person_is_named_by_an_email_or_by_an_account_and_by_one_of_them_that_is_valid_and_exists()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);

        var both = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email, accountId: worker.Id);
        var neither = await LinkOperatorAsync(setup.MainOperator, setup.Live);
        var notAnEmail = await LinkOperatorAsync(setup.MainOperator, setup.Live, email: "not an email");
        var noAccount = await LinkOperatorAsync(setup.MainOperator, setup.Live, accountId: Guid.NewGuid());

        Assert.Equal("malformed-request", await ErrorCodeAsync(both));
        Assert.Equal("malformed-request", await ErrorCodeAsync(neither));
        Assert.Equal(HttpStatusCode.BadRequest, notAnEmail.StatusCode);
        Assert.Equal("invalid-email", await ErrorCodeAsync(notAnEmail));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noAccount.StatusCode);
        Assert.Equal("account-not-found", await ErrorCodeAsync(noAccount));
        Assert.Empty(await GrantsOfEventAsync(setup.Live));
    }

    [Fact]
    public async Task The_Main_Operator_lists_the_grants_of_its_Event_and_none_of_another_with_the_emails_masked()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant, name: "Georgi Dimitrov");
        var invited = UserSeed.NewEmail("invited");
        var otherEvent = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email);
        await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: invited);
        await LinkOperatorAsync(setup.MainOperator, otherEvent, email: worker.Email);

        var response = await setup.MainOperator.Page.GetAsync($"/api/event-grants?filter=eventId eq {setup.Live}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiSessions.ReadJsonAsync(response);
        var items = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.Equal("event-grants", x.GetProperty("type").GetString()));
        Assert.All(
            items,
            x => Assert.Equal(setup.Live.ToString(), x.GetProperty("attributes").GetProperty("eventId").GetString())
        );
        Assert.Single(items, x => x.GetProperty("attributes").GetProperty("pending").GetBoolean());
        var text = body.ToString();
        Assert.DoesNotContain(worker.Email, text);
        Assert.DoesNotContain(invited, text);
        Assert.Contains("***@", text);
    }

    [Fact]
    public async Task Grants_are_listed_for_one_Event_asked_for_by_its_id_and_to_nobody_else()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);

        var noFilter = await setup.MainOperator.Page.GetAsync("/api/event-grants");
        var otherParameter = await setup.MainOperator.Page.GetAsync(
            $"/api/event-grants?filter=eventId eq {setup.Live}&tenantId=x"
        );
        var byTenant = await setup.MainOperator.Page.GetAsync("/api/event-grants?filter=tenantId eq country-bg");
        var byAMember = await setup.Member.Page.GetAsync($"/api/event-grants?filter=eventId eq {setup.Live}");
        var byTheRoot = await setup.Root.Page.GetAsync($"/api/event-grants?filter=eventId eq {setup.Live}");

        Assert.Equal(HttpStatusCode.BadRequest, noFilter.StatusCode);
        Assert.Equal("invalid-filter", await ErrorCodeAsync(noFilter));
        Assert.Equal("unsupported-parameter", await ErrorCodeAsync(otherParameter));
        Assert.Equal("invalid-filter", await ErrorCodeAsync(byTenant));
        Assert.Equal(HttpStatusCode.Forbidden, byAMember.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byTheRoot.StatusCode);
    }

    [Fact]
    public async Task Removing_a_grant_ends_the_access_at_once()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var grant = await GrantIdAsync(
            await LinkOfficialAsync(setup.MainOperator, setup.Live, "Steward", email: worker.Email)
        );
        Assert.True((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());

        var removed = await setup.MainOperator.Page.DeleteAsync($"/api/event-grants/{grant}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());
        Assert.Null(await GrantOfAsync(grant));
    }

    [Fact]
    public async Task A_grant_is_removed_by_the_Main_Operator_of_its_Event_and_to_everybody_else_it_is_not_there()
    {
        await using var api = NewApi();
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var grant = await GrantIdAsync(await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email));

        foreach (var caller in new[] { setup.Root, setup.Member, worker })
        {
            var response = await caller.Page.DeleteAsync($"/api/event-grants/{grant}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var missing = await setup.MainOperator.Page.DeleteAsync($"/api/event-grants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotNull(await GrantOfAsync(grant));
    }

    [Fact]
    public async Task A_grant_gives_nothing_on_another_Event_and_nothing_on_an_Event_that_has_ended()
    {
        await using var api = NewApi(out var time);
        using var client = ApiClients.Of(api);
        var setup = await SetUpAsync(api, client);
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, setup.Tenant);
        var otherEvent = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            setup.Tenant,
            setup.MainOperator.Id,
            DateTimeOffset.UtcNow
        );
        await LinkOperatorAsync(setup.MainOperator, setup.Live, email: worker.Email);

        Assert.True((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());
        Assert.False((await CapabilitiesAsync(worker, otherEvent)).GetProperty("canSnapshot").GetBoolean());
        time.Advance(TimeSpan.FromDays(3));
        Assert.False((await CapabilitiesAsync(worker, setup.Live)).GetProperty("canSnapshot").GetBoolean());
    }

    ApiFactory NewApi()
    {
        return NewApi(out _);
    }

    ApiFactory NewApi(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        return new ApiFactory(_mongo.ConnectionString, time: time);
    }

    /// <summary>A Tenant that has a Tenant Root, a Live Event run by a Main Operator, and the people around it.</summary>
    async Task<Scene> SetUpAsync(ApiFactory api, HttpClient client)
    {
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var member = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        return new Scene(tenant, root, mainOperator, member, developer, live);
    }

    /// <summary>A person registers with the address, as a visitor does: the details, then the code that was mailed.</summary>
    async Task<Person> RegisterAsync(ApiFactory api, string email)
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
        var page = new PageClient(ApiClients.Of(api));
        page.Set(SessionCookie.From(created)!);
        var user = (await ApiClients.FindUserAsync(_mongo.ConnectionString, email))!;
        return new Person(user["_id"].AsGuid, email, page);
    }

    static Task<HttpResponseMessage> LinkOperatorAsync(
        Person caller,
        Guid eventId,
        string? email = null,
        Guid? accountId = null
    )
    {
        return caller.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId,
                kind = "operator",
                email,
                accountId,
            }
        );
    }

    static Task<HttpResponseMessage> LinkOfficialAsync(
        Person caller,
        Guid eventId,
        string role,
        string? email = null,
        Guid? accountId = null
    )
    {
        return caller.Page.WriteAsync(
            HttpMethod.Post,
            "/api/event-grants",
            "event-grants",
            new
            {
                eventId,
                kind = "official",
                officialRole = role,
                email,
                accountId,
            }
        );
    }

    static async Task<Guid> GrantIdAsync(HttpResponseMessage response)
    {
        return Guid.Parse(
            (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("id").GetString()!
        );
    }

    async Task<BsonDocument?> GrantOfAsync(Guid id)
    {
        return await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(new BsonDocument("_id", EventSeed.Binary(id)))
            .FirstOrDefaultAsync();
    }

    async Task<List<BsonDocument>> GrantsOfEventAsync(Guid eventId)
    {
        return await EventSeed
            .Grants(_mongo.ConnectionString)
            .Find(new BsonDocument("EventId", EventSeed.Binary(eventId)))
            .ToListAsync();
    }

    static async Task<JsonElement> CapabilitiesAsync(Person person, Guid eventId)
    {
        var response = await person.Page.GetAsync($"/api/events/{eventId}/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
    }

    static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ApiSessions.ReadJsonAsync(response);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    sealed class Scene
    {
        public Scene(string tenant, Person root, Person mainOperator, Person member, Person developer, Guid live)
        {
            Tenant = tenant;
            Root = root;
            MainOperator = mainOperator;
            Member = member;
            Developer = developer;
            Live = live;
        }

        public string Tenant { get; }
        public Person Root { get; }
        public Person MainOperator { get; }
        public Person Member { get; }
        public Person Developer { get; }
        public Guid Live { get; }
    }
}
