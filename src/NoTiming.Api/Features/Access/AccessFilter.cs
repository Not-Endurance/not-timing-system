using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Events;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The one place an endpoint is authorized and its request validated (ADR-0012, #671), for every endpoint that declares what it
/// needs with <see cref="RequiresAttribute"/>. It answers in the order the rest-api skill gives: the transport of the request
/// (415, 413, 400), the place the request names, found once (400 when it is named wrongly, 404 when it is not there), the
/// Level the caller holds against the row, then the stage of the place (403, 409), then the content of the request (400,
/// 422). Only then is the handler reached, with a caller who holds the Level and a valid request, and what the handler throws
/// that the Api has an answer for is answered once, here. The caller is signed in by then: that is the baseline's, which
/// runs before any endpoint.
/// </summary>
internal sealed class AccessFilter : IEndpointFilter
{
    readonly UserManager<NIdentityUser> _users;
    readonly StandingReader _standings;
    readonly EventStore _events;
    readonly EventGrantStore _grants;
    readonly AccessContext _access;

    public AccessFilter(
        UserManager<NIdentityUser> users,
        StandingReader standings,
        EventStore events,
        EventGrantStore grants,
        AccessContext access
    )
    {
        _users = users;
        _standings = standings;
        _events = events;
        _grants = grants;
        _access = access;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<RequiresAttribute>() is not { } requires)
        {
            return await Unguarded(http, invocation, next);
        }

        var inputs = invocation.Arguments.OfType<IJsonApiInput>().ToList();
        if (inputs.Select(x => x.Transport).FirstOrDefault(x => x != null) is { } transport)
        {
            return transport;
        }

        var user = await _users.GetUserAsync(http.User);
        var (refusal, facts, grant) = await PlaceOfAsync(requires.Place, http, inputs);
        if (refusal != null)
        {
            return refusal;
        }

        var standing = await StandingAsync(user, facts, http.RequestAborted);
        var state = facts?.State ?? PlaceState.Platform;
        if (requires.SeenWith != 0 && !AccessPolicy.Decide(requires.SeenWith, standing, state).IsAllowed)
        {
            return JsonApiResults.NotFound(); // what a caller may not see is not there for it
        }

        var verdict = AccessPolicy.Decide(requires.Capability, standing, state);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        if (inputs.Select(x => x.Content()).FirstOrDefault(x => x != null) is { } invalid)
        {
            return invalid.AsResult();
        }

        _access.Fill(user, standing, facts, grant);
        try
        {
            return await next(invocation);
        }
        catch (Exception exception) when (ApiFailures.Of(exception) is { } failure)
        {
            return failure.AsResult();
        }
    }

    /// <summary>An endpoint of the group that declares nothing is let through only where the one list of public endpoints names it.</summary>
    static async ValueTask<object?> Unguarded(
        HttpContext http,
        EndpointFilterInvocationContext invocation,
        EndpointFilterDelegate next
    )
    {
        if (
            http.GetEndpoint() is RouteEndpoint route
            && PublicEndpoints.AccessOf(http.Request.Method, route.RoutePattern.RawText) != EndpointAccess.Protected
        )
        {
            return await next(invocation);
        }

        throw new InvalidOperationException(
            $"{http.Request.Method} {http.Request.Path} declares no requirement and is not public: the host should not have started."
        );
    }

    async Task<Standing> StandingAsync(NIdentityUser? user, EventFacts? facts, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return Standing.Anonymous;
        }

        return facts is null
            ? await _standings.AtPlatformAsync(user, cancellationToken)
            : await _standings.AtEventAsync(user, facts, cancellationToken);
    }

    async Task<(IResult? Refusal, EventFacts? Event, EventGrant? Grant)> PlaceOfAsync(
        PlaceIn place,
        HttpContext http,
        IReadOnlyList<IJsonApiInput> inputs
    )
    {
        switch (place)
        {
            case PlaceIn.Platform:
                return (null, null, null);
            case PlaceIn.EventInBody:
            case PlaceIn.EventInFilter:
                return await NamedEventAsync(place, inputs, http.RequestAborted);
            case PlaceIn.EventOfGrant:
            case PlaceIn.EventOfConsoleOperator:
                return await EventOfGrantAsync(place, http);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(place),
                    place,
                    "A place the filter has no way of finding."
                );
        }
    }

    async Task<(IResult?, EventFacts?, EventGrant?)> NamedEventAsync(
        PlaceIn place,
        IReadOnlyList<IJsonApiInput> inputs,
        CancellationToken cancellationToken
    )
    {
        var text = inputs.Select(x => x.NamedEvent).FirstOrDefault(x => x != null);
        if (!Guid.TryParse(text, out var id))
        {
            return (NotNamed(place), null, null);
        }

        var facts = await _events.FindAsync(id, cancellationToken);
        return facts is null ? (JsonApiResults.NotFound(), null, null) : (null, facts, null);
    }

    async Task<(IResult?, EventFacts?, EventGrant?)> EventOfGrantAsync(PlaceIn place, HttpContext http)
    {
        var wantsConsoleOperator = place == PlaceIn.EventOfConsoleOperator;
        var grant = Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var id)
            ? await _grants.FindAsync(id, http.RequestAborted)
            : null;
        if (grant is null || (grant.Kind == GrantKind.ConsoleOperator) != wantsConsoleOperator)
        {
            return (JsonApiResults.NotFound(), null, null); // the other kind is the other resource's
        }

        var facts = await _events.FindAsync(grant.EventId, http.RequestAborted);
        return facts is null ? (JsonApiResults.NotFound(), null, null) : (null, facts, grant);
    }

    static IResult NotNamed(PlaceIn place)
    {
        return place == PlaceIn.EventInFilter
            ? JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-filter",
                "The filter is not valid.",
                "Name the Event: filter=eventId eq <id>."
            )
            : JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-event",
                "The Event is not named.",
                "Name the Event by its id as eventId."
            );
    }
}
