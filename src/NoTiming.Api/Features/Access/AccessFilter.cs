using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The one place an endpoint is authorized and its request validated (ADR-0012, #671), for every endpoint that declares what it
/// needs with <see cref="RequiresAttribute"/>. It answers in the order the rest-api skill gives: the transport of the request
/// (415, 413, 400), the place the request names, found once (400 when it is named wrongly, 404 when it is not there), the
/// Level the caller holds against the row, then the stage of the place (403, 409), then the content of the request (400,
/// 409, 422). Only then is the handler reached, with a caller who holds the Level and a valid request, and what the handler
/// throws that the Api has an answer for is answered once, here. The caller is signed in by then: that is the baseline's,
/// which runs before any endpoint.
/// </summary>
internal sealed class AccessFilter : IEndpointFilter
{
    readonly UserManager<NIdentityUser> _users;
    readonly StandingReader _standings;
    readonly EventStore _events;
    readonly EventGrantStore _grants;
    readonly TenantStore _tenants;
    readonly AccessContext _access;

    public AccessFilter(
        UserManager<NIdentityUser> users,
        StandingReader standings,
        EventStore events,
        EventGrantStore grants,
        TenantStore tenants,
        AccessContext access
    )
    {
        _users = users;
        _standings = standings;
        _events = events;
        _grants = grants;
        _tenants = tenants;
        _access = access;
    }

    static IResult NotInTheFilter { get; } =
        JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter is not valid.",
            "Name the Event: filter=eventId eq <id>."
        );

    static IResult NotInTheBody { get; } =
        JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "invalid-event",
            "The Event is not named.",
            "Name the Event by its id as eventId."
        );

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
        var place = await PlaceOfAsync(requires.Place, user, http, inputs);
        if (place.Refusal != null)
        {
            return place.Refusal;
        }

        var (standing, state) = await StandingAsync(user, place, requires.Capability, http.RequestAborted);
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

        _access.Fill(user, standing, place);
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

    /// <summary>Where the caller stands at the place the capability belongs to, in the state that place is in: the Event, the Tenant or the platform.</summary>
    async Task<(Standing Standing, PlaceState State)> StandingAsync(
        NIdentityUser? user,
        FoundPlace place,
        Capability capability,
        CancellationToken cancellationToken
    )
    {
        switch (AccessPolicy.RequirementOf(capability).Place)
        {
            case Place.Event:
                var facts = place.Event ?? throw NoSuchPlace(capability, "an Event");
                return (
                    user is null ? Standing.Anonymous : await _standings.AtEventAsync(user, facts, cancellationToken),
                    facts.State
                );
            case Place.Tenant:
                var tenant = place.Tenant ?? throw NoSuchPlace(capability, "a Tenant");
                return (
                    user is null
                        ? Standing.Anonymous
                        : await _standings.AtTenantAsync(user, tenant.Id, cancellationToken),
                    PlaceState.OfTenant(place.IsOperational)
                );
            default:
                return (
                    user is null ? Standing.Anonymous : await _standings.AtPlatformAsync(user, cancellationToken),
                    PlaceState.Platform
                );
        }
    }

    static InvalidOperationException NoSuchPlace(Capability capability, string place)
    {
        return new InvalidOperationException($"{capability} is an action of {place}, and the route names none.");
    }

    async Task<FoundPlace> PlaceOfAsync(
        PlaceIn place,
        NIdentityUser? user,
        HttpContext http,
        IReadOnlyList<IJsonApiInput> inputs
    )
    {
        var cancellationToken = http.RequestAborted;
        switch (place)
        {
            case PlaceIn.Platform:
                return FoundPlace.Platform;
            case PlaceIn.EventInBody:
            case PlaceIn.EventInFilter:
                return await EventNamedAsync(
                    inputs.Select(x => x.NamedEvent).FirstOrDefault(x => x != null),
                    place == PlaceIn.EventInFilter ? NotInTheFilter : NotInTheBody,
                    cancellationToken
                );
            case PlaceIn.EventInDocument:
                return inputs.Select(x => x.DocumentId).FirstOrDefault(x => x != null) is { } documentId
                    ? await EventNamedAsync(documentId, NotInTheDocument(missing: false), cancellationToken)
                    : FoundPlace.Refused(NotInTheDocument(missing: true));
            case PlaceIn.EventRoute:
            case PlaceIn.StartedEventRoute:
                return await EventOfRouteAsync(place == PlaceIn.StartedEventRoute, http, cancellationToken);
            case PlaceIn.EventOfGrant:
            case PlaceIn.EventOfConsoleOperator:
                return await EventOfGrantAsync(place == PlaceIn.EventOfConsoleOperator, http, cancellationToken);
            case PlaceIn.TenantRoute:
                return await TenantOfRouteAsync(http, cancellationToken);
            case PlaceIn.CurrentTenant:
                return await CurrentTenantAsync(user, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(place),
                    place,
                    "A place the filter has no way of finding."
                );
        }
    }

    async Task<FoundPlace> EventNamedAsync(string? text, IResult notNamed, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(text, out var id))
        {
            return FoundPlace.Refused(notNamed);
        }

        var facts = await _events.FindAsync(id, cancellationToken);
        return facts is null ? FoundPlace.Refused(JsonApiResults.NotFound()) : FoundPlace.OfEvent(facts);
    }

    async Task<FoundPlace> EventOfRouteAsync(
        bool mustHaveStarted,
        HttpContext http,
        CancellationToken cancellationToken
    )
    {
        var facts = Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var id)
            ? await _events.FindAsync(id, cancellationToken)
            : null;

        // An Event that has not started is a Setup, which is not an Event, whoever asks.
        return facts is null || (mustHaveStarted && !facts.Record.IsStarted)
            ? FoundPlace.Refused(JsonApiResults.NotFound())
            : FoundPlace.OfEvent(facts);
    }

    async Task<FoundPlace> EventOfGrantAsync(
        bool wantsConsoleOperator,
        HttpContext http,
        CancellationToken cancellationToken
    )
    {
        var grant = Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var id)
            ? await _grants.FindAsync(id, cancellationToken)
            : null;
        if (grant is null || (grant.Kind == GrantKind.ConsoleOperator) != wantsConsoleOperator)
        {
            return FoundPlace.Refused(JsonApiResults.NotFound()); // the other kind is the other resource's
        }

        var facts = await _events.FindAsync(grant.EventId, cancellationToken);
        return facts is null ? FoundPlace.Refused(JsonApiResults.NotFound()) : FoundPlace.OfEvent(facts, grant);
    }

    async Task<FoundPlace> TenantOfRouteAsync(HttpContext http, CancellationToken cancellationToken)
    {
        var id = http.GetRouteValue("id")?.ToString();
        var tenant = id is null ? null : await _tenants.FindAsync(id, cancellationToken);
        return tenant is null
            ? FoundPlace.Refused(JsonApiResults.NotFound())
            : FoundPlace.OfTenant(tenant, await _tenants.IsOperationalAsync(tenant.Id, cancellationToken));
    }

    /// <summary>What the account does in is its current Tenant: nothing that is sent names another.</summary>
    async Task<FoundPlace> CurrentTenantAsync(NIdentityUser? user, CancellationToken cancellationToken)
    {
        var id = user is null ? null : AccountRoles.CurrentTenantOf(user);
        var tenant = id is null ? null : await _tenants.FindAsync(id, cancellationToken);
        return tenant is null
            ? FoundPlace.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status409Conflict,
                    "no-current-tenant",
                    "Choose a country on your profile before you do this: it is done in the Tenant of one."
                )
            )
            : FoundPlace.OfTenant(tenant, await _tenants.IsOperationalAsync(tenant.Id, cancellationToken));
    }

    static IResult NotInTheDocument(bool missing)
    {
        return missing
            ? JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "id-required",
                "Name the Setup the Event starts from.",
                "The id of the resource is the id of its Setup."
            )
            : JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-id",
                "The Event is not named.",
                "The id of the resource is the id of the Event, and it is a Guid."
            );
    }
}
