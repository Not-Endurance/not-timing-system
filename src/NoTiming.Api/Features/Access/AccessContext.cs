using Not.Identity;
using NoTiming.Api.Features.Events;
using NTS.Domain.Access;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;

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
    IEventScoped? _row;
    bool _isOperational;

    /// <summary>The signed-in account that asks. A handler is reached by a caller who is not signed in only when the endpoint is public.</summary>
    public NIdentityUser User =>
        _user ?? throw new InvalidOperationException("The request is not from a signed-in account.");

    /// <summary>Where the caller stands at the place the request names.</summary>
    public Standing Standing { get; private set; } = Standing.Anonymous;

    /// <summary>The Event the request names, found once; a handler of an endpoint that names none does not ask for it.</summary>
    public EventFacts Event => _event ?? throw new InvalidOperationException("The request does not name an Event.");

    /// <summary>The Tenant the request is about, found once; a handler of an endpoint that is about none does not ask for it.</summary>
    public Tenant Tenant => _tenant ?? throw new InvalidOperationException("The request is not about a Tenant.");

    /// <summary>Whether the request is about a Tenant: an account that acts in none is let through by a route that says what then decides.</summary>
    public bool HasTenant => _tenant != null;

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
        _row = place.Row;
    }

    /// <summary>The row the route names, found once, when the place of the request is the Event of one.</summary>
    public TRow RowOf<TRow>()
        where TRow : class, IEventScoped
    {
        return _row as TRow ?? throw new InvalidOperationException($"The request does not name a {typeof(TRow).Name}.");
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

    public static FoundPlace OfRow(EventFacts facts, IEventScoped row)
    {
        return new FoundPlace { Event = facts, Row = row };
    }

    public static FoundPlace OfTenant(Tenant tenant, bool isOperational)
    {
        return new FoundPlace { Tenant = tenant, IsOperational = isOperational };
    }

    /// <summary>The place of an account that acts in no Tenant, where the route lets it ask about the platform instead.</summary>
    public static FoundPlace WithoutATenant()
    {
        return new FoundPlace { IsWithoutTenant = true };
    }

    FoundPlace() { }

    public static FoundPlace Platform { get; } = new();

    public IResult? Refusal { get; private init; }
    public EventFacts? Event { get; private init; }
    public EventGrant? Grant { get; private init; }
    public IEventScoped? Row { get; private init; }
    public Tenant? Tenant { get; private init; }
    public bool IsOperational { get; private init; }

    /// <summary>The account acts in no Tenant, and what the route asks instead is asked at the platform.</summary>
    public bool IsWithoutTenant { get; private init; }
}
