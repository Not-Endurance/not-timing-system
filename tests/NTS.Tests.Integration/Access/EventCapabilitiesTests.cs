using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// <c>GET /api/events/{id}/capabilities</c> says what the caller may do about an Event, and it is the policy that says it
/// (ADR-0012, #643, #671): the Event's Tenant, its stage by the clock of the host, the roles the caller holds and what the
/// caller has been granted on it, read from the data and put to the same function that the unit tests check against the matrix.
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
        var people = new[]
        {
            await PersonAsync(api, client, "console operator", tenant, kind: "ConsoleOperator"),
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
            [EventStage.Unstarted] = await EventSeed.SetupAsync(_mongo.ConnectionString, tenant, null),
            [EventStage.Live] = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, null, now),
            [EventStage.Historic] = await EventSeed.HistoricAsync(_mongo.ConnectionString, tenant, null, now),
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
                var standing = Standing.AtEvent(person.Roles, tenant, person.GrantsOn(eventId, tenant));
                var state = PlaceState.OfEvent(stage);
                var inTheTenant = Standing.AtTenant(
                    person.Roles,
                    tenant,
                    isConsoleOperatorOfAnOpenEvent: person.Kind == "ConsoleOperator"
                );

                var said = await CapabilitiesAsync(person.Person, eventId);

                var where = $"{person.Name} at {stage}";
                Assert.True(said.GetProperty("stage").GetString() == stage.ToString().ToLowerInvariant(), where);
                Assert.True(said.GetProperty("level").GetString() == LevelText(standing.Level), where + ": level");
                Assert.True(
                    AccessPolicy.Decide(Capability.SendSnapshot, standing, state).IsAllowed
                        == said.GetProperty("canSnapshot").GetBoolean(),
                    where + ": canSnapshot"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.AssignConsoleOperators, standing, state).IsAllowed
                        == said.GetProperty("canAssignConsoleOperators").GetBoolean(),
                    where + ": canAssignConsoleOperators"
                );
                Assert.True(
                    AccessPolicy.Decide(Capability.CreateEvent, inTheTenant, PlaceState.OfTenant(true)).IsAllowed
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
        var consoleOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(
            _mongo.ConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
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
        Assert.True((await CapabilitiesAsync(consoleOperator, live)).GetProperty("canSnapshot").GetBoolean());
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
        return new Persona(name, person, Roles.Of(isDeveloper, tenantRoot), kind, role);
    }

    static async Task<JsonElement> CapabilitiesAsync(Person person, Guid eventId)
    {
        var response = await person.Page.GetAsync($"/api/events/{eventId}/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ApiSessions.ReadJsonAsync(response)).GetProperty("data").GetProperty("attributes");
    }

    /// <summary>The level as the Api tells it: the name of the level, in camel case.</summary>
    static string LevelText(AccessLevel level)
    {
        var name = level.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    sealed class Persona
    {
        public Persona(string name, Person person, Roles roles, string? kind, string? role)
        {
            Name = name;
            Person = person;
            Roles = roles;
            Kind = kind;
            Role = role;
        }

        public string Name { get; }
        public Person Person { get; }
        public Roles Roles { get; }

        /// <summary>The grant the person holds on every Event of the test: <c>ConsoleOperator</c>, <c>Operator</c> or <c>Official</c>; none for most.</summary>
        public string? Kind { get; }

        public string? Role { get; }

        public IReadOnlyList<EventGrant> GrantsOn(Guid eventId, string tenant)
        {
            return Kind switch
            {
                null => [],
                "ConsoleOperator" =>
                [
                    EventGrant.ForConsoleOperator(Guid.NewGuid(), eventId, tenant, Person.Email, Person.Id),
                ],
                "Operator" => [EventGrant.ForOperator(Guid.NewGuid(), eventId, tenant, Person.Email, Person.Id)],
                _ =>
                [
                    EventGrant.ForOfficial(
                        Guid.NewGuid(),
                        eventId,
                        tenant,
                        Enum.Parse<OfficialRole>(Role!),
                        Person.Email,
                        Person.Id
                    ),
                ],
            };
        }
    }
}
