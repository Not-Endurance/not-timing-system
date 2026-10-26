using Not.Identity;
using NoTiming.Api.Features.Events;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// What the endpoint filter hands a handler about the request it let through (ADR-0012, #671): the caller, where they stand at
/// the place the request names, and the place found once. It is filled before the handler runs and never by the handler, so a
/// handler that has one does not read the session, find the Event or ask the policy again.
/// </summary>
internal sealed class AccessContext
{
    NIdentityUser? _user;
    EventFacts? _event;

    /// <summary>The signed-in account that asks. A handler is reached by a caller who is not signed in only when the endpoint is public.</summary>
    public NIdentityUser User =>
        _user ?? throw new InvalidOperationException("The request is not from a signed-in account.");

    public bool IsSignedIn => _user != null;

    /// <summary>Where the caller stands at the place the request names.</summary>
    public Standing Standing { get; private set; } = Standing.Anonymous;

    /// <summary>The Event the request names, found once; a handler of an endpoint that names none does not ask for it.</summary>
    public EventFacts Event => _event ?? throw new InvalidOperationException("The request does not name an Event.");

    /// <summary>The grant the route names, when the place of the request is the Event of one.</summary>
    public EventGrant? Grant { get; private set; }

    internal void Fill(NIdentityUser? user, Standing standing, EventFacts? facts, EventGrant? grant)
    {
        _user = user;
        Standing = standing;
        _event = facts;
        Grant = grant;
    }
}
