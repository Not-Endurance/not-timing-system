using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.UserSessions;

/// <summary>
/// The state a person keeps per Event (#602): the Snapshots they have sent and the ones they have selected. It is the
/// caller's own. The store finds records by owner only, the routes name no owner, and a record of another person is
/// answered as a record that is not there. The resource is <c>user-sessions</c>, flat, with the Event as an attribute
/// and the state as a member; the list of the caller's records takes the filter <c>eventId eq &lt;id&gt;</c> and no
/// other, so the Event is the only thing a caller can narrow by, the <c>sort</c> of the Events and the pages of every list.
/// The id is the server's: a document that brings one is refused.
/// </summary>
internal static class UserSessionEndpoints
{
    const string USER_SESSIONS = "user-sessions";
    const string EVENT_ID = "eventId";

    public static IEndpointRouteBuilder MapUserSessions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/user-sessions", List);
        app.MapGet("/api/user-sessions/{id}", Read);
        app.MapPost("/api/user-sessions", Make);
        app.MapPatch("/api/user-sessions/{id}", Change);
        app.MapDelete("/api/user-sessions/{id}", Remove);
        return app;
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> List(
        [ListOf(EVENT_ID, EVENT_ID)] ListQuery list,
        HttpRequest request,
        AccessContext access,
        UserSessionStore store,
        CancellationToken cancellationToken
    )
    {
        Guid? eventId = null;
        if (list.EqualTo.TryGetValue(EVENT_ID, out var written))
        {
            if (!Guid.TryParse(written, out var parsed))
            {
                return InvalidFilter();
            }

            eventId = parsed;
        }

        var (sessions, hasMore) = await store.ListAsync(
            access.User.Id.ToString(),
            eventId,
            list.Descending,
            list.Skip,
            list.Size,
            cancellationToken
        );
        return JsonApiResults.Page(
            USER_SESSIONS,
            sessions.Select(x => (x.Id.ToString(), AttributesOf(x))),
            request,
            list.Size,
            list.Number,
            hasMore
        );
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> Read(
        string id,
        AccessContext access,
        UserSessionStore store,
        CancellationToken cancellationToken
    )
    {
        var session = Guid.TryParse(id, out var parsed)
            ? await store.FindAsync(access.User.Id.ToString(), parsed, cancellationToken)
            : null;
        return session is null ? JsonApiResults.NotFound() : ResourceOf(StatusCodes.Status200OK, session);
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> Make(
        JsonApiBody<NewUserSession> body,
        AccessContext access,
        UserSessionStore store,
        CancellationToken cancellationToken
    )
    {
        var attributes = body.Attributes;
        var (session, created) = await store.MakeAsync(
            access.User.Id.ToString(),
            Guid.Parse(attributes.EventId!),
            attributes.State,
            cancellationToken
        );
        return created
            ? ResourceOf(StatusCodes.Status201Created, session, $"/api/user-sessions/{session.Id}")
            : ResourceOf(StatusCodes.Status200OK, session);
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> Change(
        string id,
        JsonApiBody<UserSessionChange> body,
        AccessContext access,
        UserSessionStore store,
        CancellationToken cancellationToken
    )
    {
        var owner = access.User.Id.ToString();
        var session = Guid.TryParse(id, out var parsed)
            ? await store.FindAsync(owner, parsed, cancellationToken)
            : null;
        if (session is null)
        {
            return JsonApiResults.NotFound();
        }

        var attributes = body.Attributes;
        if (
            attributes.EventId != null
            && !(Guid.TryParse(attributes.EventId, out var named) && named == session.EventId)
        )
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "event-immutable",
                "The Event of a record does not change: make a record for the other Event."
            );
        }

        if (attributes.State != null)
        {
            session = await store.ReplaceStateAsync(owner, session.Id, attributes.State, cancellationToken);
            if (session is null)
            {
                return JsonApiResults.NotFound(); // it was deleted while this request was on its way
            }
        }

        return ResourceOf(StatusCodes.Status200OK, session);
    }

    [Requires(Capability.SignedIn)]
    static async Task<IResult> Remove(
        string id,
        AccessContext access,
        UserSessionStore store,
        CancellationToken cancellationToken
    )
    {
        var removed =
            Guid.TryParse(id, out var parsed)
            && await store.DeleteAsync(access.User.Id.ToString(), parsed, cancellationToken);
        return removed ? Results.NoContent() : JsonApiResults.NotFound();
    }

    static IResult ResourceOf(int status, UserSession session, string? location = null)
    {
        return JsonApiResults.Resource(status, USER_SESSIONS, session.Id.ToString(), AttributesOf(session), location);
    }

    static object AttributesOf(UserSession session)
    {
        return new { eventId = session.EventId, state = session.State };
    }

    static IResult InvalidFilter()
    {
        return JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter is not valid.",
            "Use eventId eq <the id of an Event>."
        );
    }
}
