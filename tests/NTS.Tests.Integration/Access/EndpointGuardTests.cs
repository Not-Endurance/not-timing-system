using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Tenancy;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Integration;

/// <summary>
/// The guard that fails closed (ADR-0012, #671): a route of the Api that is neither public nor declares what it needs is a
/// mistake. The routes are made up for each case, with no host and no database, and the real ones are held to the same
/// rule beside them.
/// </summary>
public sealed class EndpointGuardTests
{
    [Fact]
    public void A_route_of_the_Api_that_declares_nothing_and_is_not_public_is_a_mistake()
    {
        var routes = new[] { Route("GET", "/api/secrets") };

        var mistakes = EndpointGuard.Mistakes(routes);

        Assert.Equal(["GET api/secrets declares no requirement and is not public"], mistakes);
    }

    [Fact]
    public void A_route_that_declares_what_it_needs_is_guarded_whatever_it_needs()
    {
        var routes = new[]
        {
            Route("GET", "/api/secrets", new RequiresAttribute(Capability.ReadConfiguration, PlaceIn.EventInFilter)),
            Route("POST", "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
        };

        Assert.Empty(EndpointGuard.Mistakes(routes));
    }

    [Fact]
    public void A_route_the_list_of_public_endpoints_names_needs_no_requirement()
    {
        var routes = new[] { Route("GET", "/api/events/live"), Route("POST", "/api/sessions") };

        Assert.Empty(EndpointGuard.Mistakes(routes));
    }

    [Fact]
    public void A_route_that_is_about_a_row_has_to_say_which_rows_it_is_about()
    {
        var requires = new RequiresAttribute(Capability.RunEvent, PlaceIn.EventOfRow);
        var routes = new[]
        {
            Route("PATCH", "/api/rows/{id}", requires),
            Route("DELETE", "/api/rows/{id}", requires, new NoRows()),
        };

        var mistakes = EndpointGuard.Mistakes(routes);

        Assert.Equal(
            ["PATCH api/rows/{id} is about a row of what an Event keeps and does not say which rows"],
            mistakes
        );
    }

    [Fact]
    public void A_route_that_is_not_the_Api_is_not_the_guards()
    {
        var routes = new[] { Route("GET", "/sign-in"), Route("GET", "/healthz"), Route("GET", "/live-hub") };

        Assert.Empty(EndpointGuard.Mistakes(routes));
    }

    [Fact]
    public void A_route_a_host_says_is_public_needs_no_requirement_and_is_a_mistake_where_the_host_does_not_say_it()
    {
        var routes = new[] { Route("POST", "/api/dev/sessions") };
        var hostEntries = new[] { new PublicEndpoint(EndpointAccess.SignIn, "POST", "api/dev/sessions") };

        Assert.Empty(EndpointGuard.Mistakes(routes, hostEntries));
        Assert.Equal(["POST api/dev/sessions declares no requirement and is not public"], EndpointGuard.Mistakes(routes));
    }

    [Fact]
    public void The_host_is_stopped_with_the_routes_it_does_not_know_named()
    {
        var routes = new[] { Route("GET", "/api/secrets"), Route("DELETE", "/api/secrets/{id}") };

        var stopped = Assert.Throws<InvalidOperationException>(() => EndpointGuard.ThrowWhenUnguarded(routes));

        Assert.Contains("GET api/secrets declares no requirement", stopped.Message);
        Assert.Contains("DELETE api/secrets/{id} declares no requirement", stopped.Message);
    }

    [Fact]
    public void The_host_starts_when_every_route_is_known()
    {
        var routes = new[]
        {
            Route("GET", "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
            Route("GET", "/api/events/live"),
        };

        EndpointGuard.ThrowWhenUnguarded(routes);
    }

    [Fact]
    public void The_methods_of_a_route_are_guarded_one_by_one()
    {
        var routes = new[]
        {
            Route(["GET", "POST"], "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
        };

        Assert.Empty(EndpointGuard.Mistakes(routes));
    }

    static RouteEndpoint Route(string method, string pattern, params object[] metadata)
    {
        return Route([method], pattern, metadata);
    }

    static RouteEndpoint Route(string[] methods, string pattern, params object[] metadata)
    {
        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            0,
            new EndpointMetadataCollection([new HttpMethodMetadata(methods), .. metadata]),
            pattern
        );
    }

    sealed class NoRows : IEventRows
    {
        public Task<IEventScoped?> FindAsync(CrossTenantReads reads, Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEventScoped?>(null);
        }
    }
}
