using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using NoTiming.Api.Features.Access;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The name of an endpoint of the Api is derived from its method and its route (ADR-0015): the one rule, the routes that are made
/// up for each case with no host, and the real routes held to it beside them.
/// </summary>
public sealed class EndpointNamesTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public EndpointNamesTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Theory]
    [InlineData("GET", "/api/me", "GetMe")]
    [InlineData("GET", "/api/me/profile", "GetMeProfile")]
    [InlineData("POST", "/api/passkeys/actions/request-options", "PostPasskeysActionsRequestOptions")]
    [InlineData("DELETE", "/api/sessions/current", "DeleteSessionsCurrent")]
    [InlineData("GET", "/api/user-sessions/{id}", "GetUserSessionsById")]
    [InlineData("PATCH", "/api/events/{id}/branding", "PatchEventsByIdBranding")]
    [InlineData("GET", "/api/athletes/all-tenants", "GetAthletesAllTenants")]
    [InlineData("GET", "/api/events/{eventId:guid}/capabilities", "GetEventsByEventIdCapabilities")]
    [InlineData("patch", "api/tenants/{id}", "PatchTenantsById")]
    public void A_name_is_the_method_then_each_segment_of_the_route_without_api(
        string method,
        string pattern,
        string expected
    )
    {
        Assert.Equal(expected, EndpointNames.Of(method, pattern));
    }

    [Theory]
    [InlineData("PUT", "/api/things/{id}")]
    [InlineData("OPTIONS", "/api/things")]
    [InlineData("GET", "/sign-in")]
    [InlineData("GET", "/healthz")]
    [InlineData("GET", null)]
    public void A_route_that_is_not_of_the_Api_and_a_method_it_does_not_use_have_no_name(string method, string? pattern)
    {
        Assert.Null(EndpointNames.Of(method, pattern));
    }

    [Fact]
    public void An_endpoint_handled_by_a_method_that_is_named_for_its_route_is_fine()
    {
        var routes = new[]
        {
            Route("GET", "/api/things/{id}", GetThingsById),
            Route("POST", "/api/things", PostThings),
        };

        Assert.Empty(EndpointNames.Mistakes(routes));
    }

    [Fact]
    public void An_endpoint_whose_handler_is_named_for_something_else_is_told_what_it_should_be()
    {
        var routes = new[] { Route("GET", "/api/things/{id}", List) };

        var mistakes = EndpointNames.Mistakes(routes);

        Assert.Equal(["GET api/things/{id} is handled by List: it should be GetThingsById"], mistakes);
    }

    [Fact]
    public void A_lambda_has_to_be_given_the_name_the_route_gives()
    {
        var routes = new[]
        {
            Route("GET", "/api/things", () => Results.Ok()),
            Route("POST", "/api/things", () => Results.Ok(), name: "PostThings"),
            Route("DELETE", "/api/things/{id}", () => Results.Ok(), name: "Remove"),
        };

        var mistakes = EndpointNames.Mistakes(routes);

        Assert.Equal(
            [
                "GET api/things is handled by a lambda with no name: give it .WithName(EndpointNames.Of(...)), GetThings",
                "DELETE api/things/{id} is handled by Remove: it should be DeleteThingsById",
            ],
            mistakes
        );
    }

    [Fact]
    public void A_route_the_Api_changes_with_a_method_other_than_PATCH_is_a_mistake()
    {
        var routes = new[] { Route("PUT", "/api/things/{id}", GetThingsById) };

        var mistakes = EndpointNames.Mistakes(routes);

        Assert.Equal(["PUT api/things/{id} uses a method the JSON:API does not: it changes with PATCH"], mistakes);
    }

    [Fact]
    public void The_pages_of_the_host_and_the_answer_to_an_unknown_route_are_not_held_to_the_rule()
    {
        var routes = new[]
        {
            Route("GET", "/sign-in", List),
            Route("GET", "/healthz", List),
            Route("GET", "/api/{**rest}", List),
        };

        Assert.Empty(EndpointNames.Mistakes(routes));
    }

    [Fact]
    public async Task Every_route_of_the_Api_is_named_for_its_method_and_its_route()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);

        var endpoints = api.Services.GetServices<EndpointDataSource>().SelectMany(x => x.Endpoints);

        var mistakes = EndpointNames.Mistakes(endpoints);

        Assert.True(mistakes.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, mistakes));
    }

    static IResult GetThingsById() => Results.Ok();

    static IResult PostThings() => Results.Ok();

    static IResult List() => Results.Ok();

    static RouteEndpoint Route(string method, string pattern, Delegate handler, string? name = null)
    {
        var metadata = new List<object> { new HttpMethodMetadata([method]), handler.Method };
        if (name != null)
        {
            metadata.Add(new EndpointNameMetadata(name));
        }

        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            0,
            new EndpointMetadataCollection(metadata),
            pattern
        );
    }
}
