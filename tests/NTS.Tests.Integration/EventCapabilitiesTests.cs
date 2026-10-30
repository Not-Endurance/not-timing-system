using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// <c>GET /api/events/{id}/capabilities</c> says what the caller may do about an Event, and it is the policy that says it
/// (ADR-0012, #643): the Event's Tenant, its one Main Operator, its stage by the clock of the host, and what the caller has
/// been granted on it, read from the data and put to the same function that the unit tests check against the matrix.
/// Every kind of person the matrix names is asked about an Event that has not started, one that is Live and one that is
/// Historic, and what the endpoint answers is what the policy answers over the same facts.
/// </summary>
public sealed class EventCapabilitiesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public EventCapabilitiesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task What_the_endpoint_says_is_what_the_policy_says_for_every_kind_of_caller_at_every_stage()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(_mongo.ConnectionString, time: time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var people = new[]
        {
            await PersonAsync(api, client, "main operator", mainOperator),
            await PersonAsync(api, client, "member", tenant),
            await PersonAsync(api, client, "operator", tenant, kind: "Operator"),
            await PersonAsync(api, client, "steward", tenant, kind: "Official", role: "Steward"),
            await PersonAsync(api, client, "chief steward", tenant, kind: "Official", role: "ChiefSteward"),
            await PersonAsync(api, client, "ground jury", tenant, kind: "Official", role: "GroundJury"),
            await PersonAsync(
                api,
                client,
                "ground jury president",
                tenant,
                kind: "Official",
                role: "GroundJuryPresident"
            ),
            await PersonAsync(
                api,
                client,
                "veterinary official",
                tenant,
                kind: "Official",
                role: "VeterinaryCommissionMember"
            ),
            await PersonAsync(api, client, "tenant root", tenant, tenantRoot: [tenant]),
            await PersonAsync(api, client, "tenant root elsewhere", other, tenantRoot: [other]),
            await PersonAsync(api, client, "developer", other, isDeveloper: true),
            await PersonAsync(
                api,
                client,
                "operator who is a tenant root",
                tenant,
                tenantRoot: [tenant],
                kind: "Operator"
            ),
        };
        var now = time.GetUtcNow();
        var events = new Dictionary<EventStage, Guid>
        {
            [EventStage.Unstarted] = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, mainOperator.Id),
            [EventStage.Live] = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, now),
            [EventStage.Historic] = await EventSeed.HistoricAsync(
                _mongo.ConnectionString,
                tenant,
                mainOperator.Id,
                now
            ),
        };
        foreach (var person in people.Where(x => x.Kind != null))
        {
            foreach (var eventId in events.Values)
            {
                await EventSeed.GrantAsync(
                    _mongo.ConnectionString,
                    tenant,
                    eventId,
                    person.Kind!,
                    person.Role,
                    person.Person.Email,
                    person.Person.Id
                );
            }
        }

        foreach (var (stage, eventId) in events)
        {
            foreach (var person in people)
            {
                var scope = AccessScope.ForEvent(tenant, mainOperator.Id, stage, person.GrantsOn(eventId, tenant));
                var inTheTenant = AccessScope.ForTenant(tenant, isOperational: true);

                var said = await CapabilitiesAsync(person.Person, eventId);

                var where = $"{person.Name} at {stage}";
                Assert.True(said.GetProperty("stage").GetString() == stage.ToString().ToLowerInvariant(), where);
                Assert.True(
                    AccessPolicy.IsMainOperator(person.Caller, scope)
                        == said.GetProperty("isMainOperator").GetBoolean(),
                    where + ": isMainOperator"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.SendSnapshot, person.Caller, scope).IsAllowed
                        == said.GetProperty("canSnapshot").GetBoolean(),
                    where + ": canSnapshot"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.HandOverMainOperator, person.Caller, scope).IsAllowed
                        == said.GetProperty("canHandOver").GetBoolean(),
                    where + ": canHandOver"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.AssignMainOperator, person.Caller, scope).IsAllowed
                        == said.GetProperty("canAssignMainOperator").GetBoolean(),
                    where + ": canAssignMainOperator"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.CreateEvent, person.Caller, inTheTenant).IsAllowed
                        == said.GetProperty("canCreateEvents").GetBoolean(),
                    where + ": canCreateEvents"
                );
            }
        }
    }

    [Fact]
    public async Task The_people_who_may_send_a_Snapshot_to_a_Live_Event_are_the_ones_the_matrix_names_and_nobody_else()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        var allowed = 0;
        foreach (
            var (kind, role, mayRecord) in new[]
            {
                ("Operator", (string?)null, true),
                ("Official", "Steward", true),
                ("Official", "ChiefSteward", true),
                ("Official", "GroundJury", true),
                ("Official", "GroundJuryPresident", true),
                ("Official", "VeterinaryCommissionMember", false),
                ("Official", "VeterinaryCommissionPresident", false),
                ("Official", "TechnicalDelegate", false),
                ("Official", "ForeignJudge", false),
                ("Official", "ForeignVeterinaryDelegate", false),
                ("Official", "PresidentTreatingVeterinaryCommission", false),
                ("Official", "TreatingVeterinaryCommissionMember", false),
                ("Official", "VeterinaryServiceMember", false),
            }
        )
        {
            var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
            await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, live, kind, role, person.Email, person.Id);

            var can = (await CapabilitiesAsync(person, live)).GetProperty("canSnapshot").GetBoolean();

            Assert.True(mayRecord == can, $"{kind} {role}");
            allowed += can ? 1 : 0;
        }

        Assert.Equal(5, allowed);
        Assert.True((await CapabilitiesAsync(mainOperator, live)).GetProperty("canSnapshot").GetBoolean());
    }

    [Fact]
    public async Task A_Tenant_that_has_no_Tenant_Root_is_offered_no_new_Event_by_the_capabilities_even_to_the_Developer()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var unstarted = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null);

        var withoutARoot = await CapabilitiesAsync(developer, unstarted);
        await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var withARoot = await CapabilitiesAsync(developer, unstarted);

        Assert.False(withoutARoot.GetProperty("canCreateEvents").GetBoolean());
        Assert.True(withARoot.GetProperty("canCreateEvents").GetBoolean());
    }

    async Task<Persona> PersonAsync(
        ApiFactory api,
        HttpClient client,
        string name,
        string home,
        string[]? tenantRoot = null,
        bool isDeveloper = false,
        string? kind = null,
        string? role = null
    )
    {
        var person = await SignedInAsync(
            api,
            client,
            _mongo.ConnectionString,
            home,
            tenantRoot == null ? null : TenantRootOf(tenantRoot),
            isDeveloper
        );
        return new Persona(name, person, Caller.Of(person.Id, isDeveloper, tenantRoot), kind, role);
    }

    /// <summary>A person who exists already, such as the Main Operator, who holds no grant.</summary>
    static Task<Persona> PersonAsync(ApiFactory api, HttpClient client, string name, Person person)
    {
        return Task.FromResult(new Persona(name, person, Caller.Of(person.Id), null, null));
    }

    static async Task<JsonElement> CapabilitiesAsync(Person person, Guid eventId)
    {
        var response = await person.Page.GetAsync($"/api/events/{eventId}/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
    }

    sealed class Persona
    {
        public Persona(string name, Person person, Caller caller, string? kind, string? role)
        {
            Name = name;
            Person = person;
            Caller = caller;
            Kind = kind;
            Role = role;
        }

        public string Name { get; }
        public Person Person { get; }
        public Caller Caller { get; }

        /// <summary>The grant the person holds on every Event of the test: <c>Operator</c> or <c>Official</c>; none for most.</summary>
        public string? Kind { get; }

        public string? Role { get; }

        public CallerGrants GrantsOn(Guid eventId, string tenant)
        {
            if (Kind == null)
            {
                return CallerGrants.None;
            }

            var grant =
                Kind == "Operator"
                    ? EventGrant.ForOperator(Guid.NewGuid(), eventId, tenant, Person.Email, Person.Id)
                    : EventGrant.ForOfficial(
                        Guid.NewGuid(),
                        eventId,
                        tenant,
                        Enum.Parse<OfficialRole>(Role!),
                        Person.Email,
                        Person.Id
                    );
            return CallerGrants.Of([grant]);
        }
    }
}
