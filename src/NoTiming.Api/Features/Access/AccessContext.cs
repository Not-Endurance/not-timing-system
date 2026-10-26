using Not.Identity;
using NoTiming.Api.Features.Events;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// What the endpoint filter hands a handler about the request it let through (ADR-0012, #671): the caller, where they stand at
/// the place the request names, and the place found once. It is filled before the handler runs and never by the handler, so a
/// handler that has one does not read the session, find the Event or the Tenant, or ask the policy again.
/// </summary>
internal sealed class AccessContext
{
    NIdentityUser? _user;
    EventFacts? _event;
    Tenant? _tenant;
    bool _isOperational;

    /// <summary>The signed-in account that asks. A handler is reached by a caller who is not signed in only when the endpoint is public.</summary>
    public NIdentityUser User =>
        _user ?? throw new InvalidOperationException("The request is not from a signed-in account.");

    public bool IsSignedIn => _user != null;

    /// <summary>Where the caller stands at the place the request names.</summary>
    public Standing Standing { get; private set; } = Standing.Anonymous;

    /// <summary>The Event the request names, found once; a handler of an endpoint that names none does not ask for it.</summary>
    public EventFacts Event => _event ?? throw new InvalidOperationException("The request does not name an Event.");

    /// <summary>The Tenant the request is about, found once; a handler of an endpoint that is about none does not ask for it.</summary>
    public Tenant Tenant => _tenant ?? throw new InvalidOperationException("The request is not about a Tenant.");

    /// <summary>Whether the Tenant the request is about can hold Events, which it can once it has a Tenant Root.</summary>
    public bool IsOperational => _tenant != null && _isOperational;

    /// <summary>The grant the route names, when the place of the request is the Event of one.</summary>
    public EventGrant? Grant { get; private set; }

    internal void Fill(NIdentityUser? user, Standing standing, FoundPlace place)
    {
        _user = user;
        Standing = standing;
        _event = place.Event;
        _tenant = place.Tenant;
        _isOperational = place.IsOperational;
        Grant = place.Grant;
    }
}

/// <summary>The place a request names, found: the Event, the Tenant or the grant, or the answer that refuses the request because of it.</summary>
internal sealed class FoundPlace
{
    public static FoundPlace Refused(IResult refusal)
    {
        return new FoundPlace { Refusal = refusal };
    }

    public static FoundPlace OfEvent(EventFacts facts, EventGrant? grant = null)
    {
        return new FoundPlace { Event = facts, Grant = grant };
    }

    public static FoundPlace OfTenant(Tenant tenant, bool isOperational)
    {
        return new FoundPlace { Tenant = tenant, IsOperational = isOperational };
    }

    FoundPlace() { }

    public static FoundPlace Platform { get; } = new();

    public IResult? Refusal { get; private init; }
    public EventFacts? Event { get; private init; }
    public EventGrant? Grant { get; private init; }
    public Tenant? Tenant { get; private init; }
    public bool IsOperational { get; private init; }
}
