using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The routes of the Api that read and the routes that write, spelled out (#643, ADR-0012). A route that is not on the
/// lists has not been reviewed for what matters about it: that no write gives a role of a Tenant or of the platform, which
/// only the Developer's command does, and that no read can be turned across Tenants by what is sent with it, because what
/// reaches across Tenants is a named view and never a parameter. Adding a route is a change to this test, and so a
/// decision about both.
/// </summary>
public sealed class RoutesOfTheApiTests : IClassFixture<MongoFixture>
{
    // What is sent to every read to try to turn it across Tenants. A route either refuses these or answers as it did without them.
    const string ACROSS = "tenantId=country-xx&tenant=country-xx&allTenants=true&acrossTenants=true&all=true";

    readonly MongoFixture _mongo;

    public RoutesOfTheApiTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_routes_that_change_state_are_exactly_the_ones_listed_and_none_is_named_for_a_role()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);

        var actual = RoutesOf(api).Where(x => x.Method is "POST" or "PUT" or "PATCH" or "DELETE").Select(x => x.Text);

        Assert.Equal(Writes().Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        Assert.All(
            Writes(),
            route =>
                Assert.DoesNotContain(NeverInARoute(), word => route.Contains(word, StringComparison.OrdinalIgnoreCase))
        );
    }

    [Fact]
    public async Task The_routes_that_read_are_exactly_the_ones_listed_besides_the_public_views()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);

        var actual = RoutesOf(api)
            .Where(x => x.Method == "GET" && x.Pattern.StartsWith("api/", StringComparison.Ordinal))
            .Select(x => x.Text);

        Assert.Equal(Reads().Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task No_read_takes_a_Tenant_or_a_flag_for_every_Tenant_as_a_parameter()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var worker = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var live = await EventSeed.LiveAsync(_mongo.ConnectionString, tenant, root.Id, DateTimeOffset.UtcNow);
        await EventSeed.GrantAsync(_mongo.ConnectionString, tenant, live, "Operator", null, worker.Email, worker.Id);
        var club = await RegistrySeed.ClubAsync(_mongo.ConnectionString, tenant, "Reads");
        var horse = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, "Reads");
        var athlete = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            "Reads",
            RegistrySeed.CountryOf("Bulgaria", "BG")
        );
        var country = await CountrySeed.AddAsync(_mongo.ConnectionString, "Reads", CountrySeed.UniqueIsoCode());
        var urls = new[]
        {
            "/api/clubs",
            $"/api/clubs/{club}",
            "/api/horses",
            $"/api/horses/{horse}",
            "/api/athletes",
            $"/api/athletes/{athlete}",
            "/api/countries",
            $"/api/countries/{country}",
            "/api/configure-events",
            $"/api/configure-events/{live}",
            "/api/events",
            "/api/events/live",
            "/api/events/historic",
            $"/api/events/{live}",
            "/api/me",
            "/api/me/profile",
            "/api/passkeys",
            "/api/user-sessions",
            $"/api/user-sessions/{Guid.NewGuid()}",
            $"/api/tenants/{tenant}",
            $"/api/tenants/{tenant}/capabilities",
            $"/api/events/{live}/capabilities",
            $"/api/event-grants?filter=eventId eq {live}",
            "/api/accounts?filter=contains(name,'abc')",
            "/api/accounts/all-tenants?filter=contains(name,'abc')",
            "/api/athletes/all-tenants?filter=contains(name,'abc')",
            "/api/horses/all-tenants?filter=contains(name,'abc')",
            "/api/clubs/all-tenants?filter=contains(name,'abc')",
            "/api/officials/all-tenants?filter=contains(name,'abc')",
        };

        foreach (var url in urls)
        {
            var plain = await root.Page.GetAsync(url);
            var plainBody = await plain.Content.ReadAsStringAsync();
            var widened = await root.Page.GetAsync(url + (url.Contains('?') ? "&" : "?") + ACROSS);
            var widenedBody = await widened.Content.ReadAsStringAsync();

            if (widened.StatusCode == HttpStatusCode.BadRequest && widenedBody.Contains("unsupported-parameter"))
            {
                continue; // refused outright: no parameter is taken
            }

            Assert.True(plain.StatusCode == widened.StatusCode, $"{url} answered differently with the parameters");
            Assert.True(plainBody == widenedBody, $"{url} answered differently with the parameters");
        }
    }

    [Fact]
    public async Task The_search_across_Tenants_is_a_route_of_its_own_and_the_search_in_a_Tenant_cannot_be_widened_into_it()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var root = await SignedInAsync(api, client, _mongo.ConnectionString, tenant, TenantRootOf(tenant));
        var marker = Guid.NewGuid().ToString("N")[..8];
        await AccountAsync(_mongo.ConnectionString, other, name: $"Foreign{marker} Person");

        var inTheTenant = await root.Page.GetAsync($"/api/accounts?filter=contains(name,'foreign{marker}')");
        var widened = await root.Page.GetAsync(
            $"/api/accounts?filter=contains(name,'foreign{marker}')&allTenants=true"
        );
        var named = await root.Page.GetAsync($"/api/accounts/all-tenants?filter=contains(name,'foreign{marker}')");

        Assert.Empty((await ApiSessions.ReadJsonAsync(inTheTenant)).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, widened.StatusCode);
        Assert.Single((await ApiSessions.ReadJsonAsync(named)).GetProperty("data").EnumerateArray());
    }

    /// <summary>Every route of the Api that changes state. None of them gives a role: the roles are written by the Developer's command.</summary>
    static string[] Writes()
    {
        return
        [
            "DELETE api/athletes/{id}",
            "DELETE api/clubs/{id}",
            "DELETE api/configure-events/{id}",
            "DELETE api/event-grants/{id}",
            "DELETE api/events/{id}",
            "DELETE api/horses/{id}",
            "DELETE api/passkeys/{id}",
            "DELETE api/sessions/current",
            "DELETE api/user-sessions/{id}",
            "PATCH api/athletes/{id}",
            "PATCH api/clubs/{id}",
            "PATCH api/configure-events/{id}",
            "PATCH api/countries/{id}",
            "PATCH api/events/{id}",
            "PATCH api/horses/{id}",
            "PATCH api/me",
            "PATCH api/me/profile",
            "PATCH api/passkeys/{id}",
            "PATCH api/tenants/{id}",
            "PATCH api/user-sessions/{id}",
            "POST api/athletes",
            "POST api/clubs",
            "POST api/code-challenges",
            "POST api/configure-events",
            "POST api/countries",
            "POST api/event-grants",
            "POST api/events",
            "POST api/events/{id}/actions/assign-main-operator",
            "POST api/events/{id}/actions/hand-over",
            "POST api/horses",
            "POST api/passkeys",
            "POST api/passkeys/actions/creation-options",
            "POST api/passkeys/actions/request-options",
            "POST api/registrations",
            "POST api/sessions",
            "POST api/user-sessions",
        ];
    }

    /// <summary>Every route of the Api that reads, the public views of the Events included, but the Ui and the hub.</summary>
    static string[] Reads()
    {
        return
        [
            "GET api/accounts",
            "GET api/accounts/all-tenants",
            "GET api/athletes",
            "GET api/athletes/all-tenants",
            "GET api/athletes/{id}",
            "GET api/clubs",
            "GET api/clubs/all-tenants",
            "GET api/clubs/{id}",
            "GET api/configure-events",
            "GET api/configure-events/{id}",
            "GET api/countries",
            "GET api/countries/{id}",
            "GET api/event-grants",
            "GET api/events",
            "GET api/events/historic",
            "GET api/events/live",
            "GET api/events/{id}",
            "GET api/events/{id}/capabilities",
            "GET api/horses",
            "GET api/horses/all-tenants",
            "GET api/horses/{id}",
            "GET api/me",
            "GET api/me/profile",
            "GET api/officials/all-tenants",
            "GET api/passkeys",
            "GET api/tenants/{id}",
            "GET api/tenants/{id}/capabilities",
            "GET api/user-sessions",
            "GET api/user-sessions/{id}",
        ];
    }

    static string[] NeverInARoute()
    {
        return ["tenant-root", "tenantroot", "developer", "membership", "roles"];
    }

    static List<RouteFacts> RoutesOf(ApiFactory api)
    {
        var routes = new List<RouteFacts>();
        foreach (var source in api.Services.GetServices<EndpointDataSource>())
        {
            foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
            {
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
                var pattern = endpoint.RoutePattern.RawText!.TrimStart('/');
                if (
                    methods is not { Count: > 0 }
                    || pattern is "live-hub" or "live-hub/negotiate"
                    || pattern.StartsWith("{**", StringComparison.Ordinal)
                )
                {
                    continue; // the hub, the Ui and the answer to an unknown route belong to no list here
                }

                routes.AddRange(methods.Where(x => x != "HEAD").Select(x => new RouteFacts(x, pattern)));
            }
        }

        return [.. routes.Where(x => x.Pattern.StartsWith("api/", StringComparison.Ordinal))];
    }

    sealed class RouteFacts
    {
        public RouteFacts(string method, string pattern)
        {
            Method = method;
            Pattern = pattern;
        }

        public string Method { get; }
        public string Pattern { get; }
        public string Text => $"{Method} {Pattern}";
    }
}
