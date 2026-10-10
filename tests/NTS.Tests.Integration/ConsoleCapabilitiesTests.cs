using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using NTS.Domain.Access;
using NTS.Domain.Enums;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// What the Console offers it asks of <c>GET /api/events/{id}/capabilities</c> (#646, ADR-0012) and does not work out for
/// itself: to configure an Event, to edit its Setup, the data it keeps, to link accounts, to brand it and to reset it. Each
/// is the policy's own answer for the caller and the Event as they are now, at every stage, for every kind of person the
/// matrix names, so that what a button offers and what the Api accepts cannot differ.
/// </summary>
public sealed class ConsoleCapabilitiesTests : IClassFixture<MongoFixture>
{
    static readonly (string Member, Capability Capability)[] FLAGS =
    [
        ("canConfigure", Capability.ConfigureEvent),
        ("canEditSetup", Capability.EditSetup),
        ("canEditData", Capability.EditEventData),
        ("canLinkAccounts", Capability.LinkAccounts),
        ("canEditBranding", Capability.EditEventBranding),
        ("canReset", Capability.ResetEvent),
    ];

    readonly MongoFixture _mongo;

    public ConsoleCapabilitiesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task What_the_Console_may_do_is_what_the_policy_says_for_every_kind_of_caller_at_every_stage()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new ApiFactory(_mongo.ConnectionString, time: time);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var people = new[]
        {
            new Persona("main operator", mainOperator, Caller.Of(mainOperator.Id), null, null),
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

        var offered = 0;
        foreach (var (stage, eventId) in events)
        {
            foreach (var person in people)
            {
                var scope = AccessScope.ForEvent(tenant, mainOperator.Id, stage, person.GrantsOn(eventId, tenant));

                var said = await CapabilitiesAsync(person.Person, eventId);

                foreach (var (member, capability) in FLAGS)
                {
                    var allowed = AccessPolicy.Decide(capability, person.Caller, scope).IsAllowed;
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
    public async Task Only_the_Main_Operator_is_offered_the_Console_of_a_Live_Event_and_a_Tenant_Root_who_assigned_it_away_is_not()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var mainOperator = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var tenantRoot = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var developer = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, isDeveloper: true);
        var operatorGrant = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var official = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, mainOperator.Id, DateTimeOffset.UtcNow);
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Operator",
            null,
            operatorGrant.Email,
            operatorGrant.Id
        );
        await EventSeed.GrantAsync(
            _mongo.ConnectionString,
            tenant,
            live,
            "Official",
            "Steward",
            official.Email,
            official.Id
        );

        var results = new Dictionary<string, bool>
        {
            ["main operator"] = (await CapabilitiesAsync(mainOperator, live))
                .GetProperty("isMainOperator")
                .GetBoolean(),
            ["tenant root"] = (await CapabilitiesAsync(tenantRoot, live)).GetProperty("isMainOperator").GetBoolean(),
            ["developer"] = (await CapabilitiesAsync(developer, live)).GetProperty("isMainOperator").GetBoolean(),
            ["operator"] = (await CapabilitiesAsync(operatorGrant, live)).GetProperty("isMainOperator").GetBoolean(),
            ["official"] = (await CapabilitiesAsync(official, live)).GetProperty("isMainOperator").GetBoolean(),
        };

        Assert.Equal(["main operator"], results.Where(x => x.Value).Select(x => x.Key));
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
