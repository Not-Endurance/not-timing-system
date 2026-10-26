using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NoTiming.Api.Features.Access;
using NTS.Domain.Access;

namespace NTS.Tests.Integration;

/// <summary>
/// The guard that fails closed (ADR-0012, #671): a route of the Api that is neither public nor declares what it needs is a
/// mistake, and so is a route named as not yet guarded that is guarded, or that is not a route. The routes are made up for
/// each case, with no host and no database, and the real ones are held to the same rule beside them.
/// </summary>
public sealed class EndpointGuardTests
{
    static readonly IReadOnlySet<string> NONE = new HashSet<string>();

    [Fact]
    public void A_route_of_the_Api_that_declares_nothing_and_is_not_public_is_a_mistake()
    {
        var routes = new[] { Route("GET", "/api/secrets") };

        var mistakes = EndpointGuard.Mistakes(routes, NONE);

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

        Assert.Empty(EndpointGuard.Mistakes(routes, NONE));
    }

    [Fact]
    public void A_route_the_list_of_public_endpoints_names_needs_no_requirement()
    {
        var routes = new[] { Route("GET", "/api/events/live"), Route("POST", "/api/sessions") };

        Assert.Empty(EndpointGuard.Mistakes(routes, NONE));
    }

    [Fact]
    public void A_route_that_is_not_the_Api_is_not_the_guards()
    {
        var routes = new[] { Route("GET", "/sign-in"), Route("GET", "/healthz"), Route("GET", "/live-hub") };

        Assert.Empty(EndpointGuard.Mistakes(routes, NONE));
    }

    [Fact]
    public void A_route_named_as_not_yet_guarded_is_let_through_until_it_is_guarded()
    {
        var routes = new[] { Route("GET", "/api/secrets") };

        var mistakes = EndpointGuard.Mistakes(routes, new HashSet<string> { "GET api/secrets" });

        Assert.Empty(mistakes);
    }

    [Fact]
    public void A_route_that_is_guarded_and_still_named_as_not_yet_guarded_is_a_mistake_so_the_list_only_shrinks()
    {
        var routes = new[]
        {
            Route("GET", "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
        };

        var mistakes = EndpointGuard.Mistakes(routes, new HashSet<string> { "GET api/secrets" });

        Assert.Equal(["GET api/secrets is guarded already and is still named as not yet guarded"], mistakes);
    }

    [Fact]
    public void A_name_that_is_not_a_route_is_a_mistake_so_a_route_that_is_gone_leaves_the_list()
    {
        var routes = new[]
        {
            Route("GET", "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
        };

        var mistakes = EndpointGuard.Mistakes(routes, new HashSet<string> { "DELETE api/gone" });

        Assert.Equal(["DELETE api/gone is named as not yet guarded and is not a route"], mistakes);
    }

    [Fact]
    public void The_host_is_stopped_with_the_routes_it_does_not_know_named()
    {
        var routes = new[] { Route("GET", "/api/secrets"), Route("DELETE", "/api/secrets/{id}") };

        var stopped = Assert.Throws<InvalidOperationException>(() => EndpointGuard.ThrowWhenUnguarded(routes, NONE));

        Assert.Contains("GET api/secrets declares no requirement", stopped.Message);
        Assert.Contains("DELETE api/secrets/{id} declares no requirement", stopped.Message);
    }

    [Fact]
    public void The_host_starts_when_every_route_is_known()
    {
        var routes = new[]
        {
            Route("GET", "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
            Route("GET", "/api/old"),
        };

        EndpointGuard.ThrowWhenUnguarded(routes, new HashSet<string> { "GET api/old" });
    }

    [Fact]
    public void The_methods_of_a_route_are_guarded_one_by_one()
    {
        var routes = new[]
        {
            Route(["GET", "POST"], "/api/secrets", new RequiresAttribute(Capability.SignedIn, PlaceIn.Platform)),
        };

        Assert.Empty(EndpointGuard.Mistakes(routes, NONE));
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
}
