using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>Where a request names the place its requirement is about (ADR-0012, #671).</summary>
internal enum PlaceIn
{
    /// <summary>No place: the platform.</summary>
    Platform = 1,

    /// <summary>The Event whose id is the route value <c>id</c>.</summary>
    EventRoute,

    /// <summary>The Event whose id is the route value <c>id</c>, once it has started: until then it is a Setup, which is not an Event.</summary>
    StartedEventRoute,

    /// <summary>The Event (the Setup it starts from) whose id is the id of the resource the document carries.</summary>
    EventInDocument,

    /// <summary>The Event named as the attribute <c>eventId</c> of the document in the body.</summary>
    EventInBody,

    /// <summary>The Event named at the head of the filter of a list: <c>filter=eventId eq {id}</c>.</summary>
    EventInFilter,

    /// <summary>The Event of the Operator or Official grant whose id is the route value <c>id</c>.</summary>
    EventOfGrant,

    /// <summary>The Event of the Console Operator grant whose id is the route value <c>id</c>.</summary>
    EventOfConsoleOperator,

    /// <summary>The Event of the row, of what an Event keeps, whose id is the route value <c>id</c>; the endpoint says which rows with <see cref="IEventRows"/>.</summary>
    EventOfRow,

    /// <summary>The Tenant whose id is the route value <c>id</c>.</summary>
    TenantRoute,

    /// <summary>The Tenant the account acts in: its selected one, or its home.</summary>
    CurrentTenant,
}

/// <summary>
/// What an endpoint needs, and where the place of it is named (ADR-0012, #671): the one thing an endpoint says about who may
/// call it. The endpoint filter does the rest, in the order of the answers: it finds the caller, the place and the caller's
/// Level there, asks the policy, and only then runs the validation of the request and the handler. A handler that is
/// reached has a caller that holds the Level and a request that is valid. An endpoint that declares none is either public,
/// by the one list, or does not let the host start.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequiresAttribute : Attribute
{
    public RequiresAttribute(Capability capability, PlaceIn place)
    {
        Capability = capability;
        Place = place;
    }

    public Capability Capability { get; }
    public PlaceIn Place { get; }

    /// <summary>
    /// What a caller has to hold to see that the thing is there at all, such as a grant: one that does not is told it is not found,
    /// and one that does is told what it is missing. None when the thing is there for everybody who may ask (the default).
    /// </summary>
    public Capability SeenWith { get; init; }

    /// <summary>
    /// Where the place is <see cref="PlaceIn.CurrentTenant"/>: what is asked instead, at the platform, of an account that acts in
    /// no Tenant, which is then let ask and finds nobody. None (the default) refuses such an account: it has to choose a Tenant.
    /// </summary>
    public Capability WithoutTenant { get; init; }
}

/// <summary>
/// The type of the resource the body of a route carries, for the routes that are made for each of several resources (the
/// families), where the attributes alone do not say it. It is metadata of the endpoint: on a handler, or added where the route is mapped.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ResourceAttribute : Attribute
{
    public ResourceAttribute(string type)
    {
        Type = type;
    }

    public string Type { get; }
}
