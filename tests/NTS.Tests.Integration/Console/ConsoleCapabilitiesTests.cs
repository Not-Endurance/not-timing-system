using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What the Console offers it asks of <c>GET /api/events/{id}/capabilities</c> (#646, ADR-0012, #671) and does not work out
/// for itself: to configure an Event, to edit its Setup, to run it (the data it keeps, and resetting it) and to assign its
/// Console Operators. Each is the policy's own answer for the caller and the Event as they are now, at every stage, for
/// every kind of person the matrix names, so that what a button offers and what the Api accepts cannot differ.
/// </summary>
public sealed class ConsoleCapabilitiesTests : IClassFixture<SharedApiFixture>
{
    static readonly (string Member, Capability Capability)[] FLAGS =
    [
        ("canConfigure", Capability.ConfigureEvent),
        ("canEditSetup", Capability.EditSetup),
        ("canRun", Capability.RunEvent),
        ("canAssignConsoleOperators", Capability.AssignConsoleOperators),
    ];

    readonly SharedApiFixture _shared;

    public ConsoleCapabilitiesTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task What_the_Console_may_do_is_what_the_policy_says_for_every_kind_of_caller_at_every_stage()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(_shared.MongoConnectionString, time: time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var other = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var people = new[]
        {
            await PersonAsync(api, client, "console operator", tenant, kind: "ConsoleOperator"),
            await PersonAsync(api, client, "member", tenant),
            await PersonAsync(api, client, "operator", tenant, kind: "Operator"),
            await PersonAsync(api, client, "steward", tenant, kind: "Official", role: "Steward"),
            await PersonAsync(api, client, "tenant root", tenant, tenantRoot: [tenant]),
            await PersonAsync(api, client, "tenant root elsewhere", other, tenantRoot: [other]),
            await PersonAsync(api, client, "developer", other, isDeveloper: true),
        };
        var now = time.GetUtcNow();
        var events = new Dictionary<EventStage, Guid>
        {
            [EventStage.Unstarted] = await EventSeed.SetupAsync(_shared.MongoConnectionString, tenant, null),
            [EventStage.Live] = await EventSeed.LiveAsync(_shared.MongoConnectionString, tenant, null, now),
            [EventStage.Historic] = await EventSeed.HistoricAsync(_shared.MongoConnectionString, tenant, null, now),
        };
        foreach (var person in people.Where(x => x.Kind != null))
        {
            foreach (var eventId in events.Values)
            {
                await EventSeed.GrantAsync(
                    _shared.MongoConnectionString,
                    tenant,
                    eventId,
                    person.Kind!,
                    person.Role,
                    person.Person.Email,
                    person.Person.Id
                );
            }
        }

        var offered = 0;
        foreach (var (stage, eventId) in events)
        {
            foreach (var person in people)
            {
                var standing = Standing.AtEvent(person.Roles, tenant, person.GrantsOn(eventId, tenant));
                var state = PlaceState.OfEvent(stage);

                var said = await CapabilitiesAsync(person.Person, eventId);

                foreach (var (member, capability) in FLAGS)
                {
                    var allowed = AccessPolicy.Decide(capability, standing, state).IsAllowed;
                    Assert.True(
                        allowed == said.GetProperty(member).GetBoolean(),
                        $"{person.Name} at {stage}: {member}"
                    );
                    offered += allowed ? 1 : 0;
                }
            }
        }

        Assert.True(offered > 0); // the matrix does offer something, so the comparison above is not of nothing
    }

    [Fact]
    public async Task Console_Operators_and_Root_are_offered_the_Console_of_a_Live_Event_and_Operators_and_Officials_are_not()
    {
        var api = _shared.Api;
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var consoleOperator = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var tenantRoot = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant, isDeveloper: true);
        var operatorGrant = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var official = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var live = await EventSeed.LiveAsync(
            _shared.MongoConnectionString,
            tenant,
            consoleOperator.Id,
            DateTimeOffset.UtcNow
        );
        await EventSeed.GrantAsync(
            _shared.MongoConnectionString,
            tenant,
            live,
            "Operator",
            null,
            operatorGrant.Email,
            operatorGrant.Id
        );
        await EventSeed.GrantAsync(
            _shared.MongoConnectionString,
            tenant,
            live,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );

        var levels = new Dictionary<string, string?>
        {
            ["console operator"] = (await CapabilitiesAsync(consoleOperator, live)).GetProperty("level").GetString(),
            ["tenant root"] = (await CapabilitiesAsync(tenantRoot, live)).GetProperty("level").GetString(),
            ["developer"] = (await CapabilitiesAsync(developer, live)).GetProperty("level").GetString(),
            ["operator"] = (await CapabilitiesAsync(operatorGrant, live)).GetProperty("level").GetString(),
            ["official"] = (await CapabilitiesAsync(official, live)).GetProperty("level").GetString(),
        };

        Assert.Equal(
            new Dictionary<string, string?>
            {
                ["console operator"] = "consoleOperator",
                ["tenant root"] = "root",
                ["developer"] = "root",
                ["operator"] = "staff",
                ["official"] = "staff",
            },
            levels
        );
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
            _shared.MongoConnectionString,
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
