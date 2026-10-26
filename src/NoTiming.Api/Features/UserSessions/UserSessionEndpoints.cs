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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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
        return JsonApiResults.Collection(
            USER_SESSIONS,
            sessions.Select(x => (x.Id.ToString(), AttributesOf(x))),
            links: JsonApiPaging.Links(request, list.Number, hasMore),
            meta: JsonApiPaging.Meta(list.Size, list.Number)
        );
    }

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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

    [Requires(Capability.SignedIn, PlaceIn.Platform)]
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

/// <summary>What a document of a user session may bring: the Event it is for and its state.</summary>
internal class UserSessionAttributes : IResourceAttributes
{
    public static string? ResourceType => "user-sessions";

    /// <summary>A text and not a Guid, so that an id that is not one is refused with a code of its own.</summary>
    public string? EventId { get; set; }

    public NtsUserSessionStateModel? State { get; set; }

    public virtual JsonApiFailure? Invalid()
    {
        return State != null && !IsWhole(State)
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-state",
                "The state is not valid.",
                "Send both lists, and no null in them or in the entries of a group."
            )
            : null;
    }

    /// <summary>A state has both its lists, and no list and no entry in one is null: JSON can say that, the model cannot.</summary>
    static bool IsWhole(NtsUserSessionStateModel state)
    {
        return state.SnapshotHistory != null
            && state.SnapshotSelections != null
            && state.SnapshotHistory.All(group => group?.Entries != null && group.Entries.All(entry => entry != null))
            && state.SnapshotSelections.All(entry => entry != null);
    }
}

/// <summary>A record that is made: for an Event, whose id is the server's to make.</summary>
internal sealed class NewUserSession : UserSessionAttributes, IResourceAttributes
{
    public static DocumentIds Ids => DocumentIds.Forbidden;

    public override JsonApiFailure? Invalid()
    {
        return !Guid.TryParse(EventId, out var eventId) || eventId == Guid.Empty
            ? new JsonApiFailure(
                StatusCodes.Status400BadRequest,
                "invalid-event",
                "Name the Event the state is for by its id."
            )
            : base.Invalid();
    }
}

/// <summary>A change of a record: the state it should have, and the Event it is for when the document says so, which has to be the one it has.</summary>
internal sealed class UserSessionChange : UserSessionAttributes { }
