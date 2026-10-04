using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Watcher.Models;

namespace NoTiming.Api.Features.UserSessions;

/// <summary>
/// The state a person keeps per Event (#602): the Snapshots they have sent and the ones they have selected. It is the
/// caller's own. The store finds records by owner only, the routes name no owner, and a record of another person is
/// answered as a record that is not there. The resource is <c>user-sessions</c>, flat, with the Event as an attribute
/// and the state as a member; the list of the caller's records takes the filter <c>eventId eq &lt;id&gt;</c> and no
/// other, so the Event is the only thing a caller can narrow by. The id is the server's: a document that brings one
/// is refused.
/// </summary>
internal static class UserSessionEndpoints
{
    const string USER_SESSIONS = "user-sessions";
    const string EVENT_ID = "eventId";

    public static IEndpointRouteBuilder MapUserSessions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/user-sessions", List).RequireAuthorization();
        app.MapGet("/api/user-sessions/{id}", Read).RequireAuthorization();
        app.MapPost("/api/user-sessions", Make).RequireAuthorization();
        app.MapPatch("/api/user-sessions/{id}", Change).RequireAuthorization();
        app.MapDelete("/api/user-sessions/{id}", Remove).RequireAuthorization();
        return app;
    }

    static async Task<IResult> List(HttpContext context, UserManager<NIdentityUser> users, UserSessionStore store)
    {
        var filter = JsonApiQuery.Read(context.Request, EVENT_ID);
        if (filter.Refusal != null)
        {
            return filter.Refusal;
        }

        Guid? eventId = null;
        if (filter.EqualTo.TryGetValue(EVENT_ID, out var written))
        {
            if (!Guid.TryParse(written, out var parsed))
            {
                return InvalidFilter();
            }

            eventId = parsed;
        }

        var sessions = await store.ListAsync(users.GetUserId(context.User)!, eventId, context.RequestAborted);
        return JsonApiResults.Collection(USER_SESSIONS, sessions.Select(x => (x.Id.ToString(), AttributesOf(x))));
    }

    static async Task<IResult> Read(
        HttpContext context,
        UserManager<NIdentityUser> users,
        UserSessionStore store,
        string id
    )
    {
        var session = Guid.TryParse(id, out var parsed)
            ? await store.FindAsync(users.GetUserId(context.User)!, parsed, context.RequestAborted)
            : null;
        return session is null ? JsonApiResults.NotFound() : ResourceOf(StatusCodes.Status200OK, session);
    }

    static async Task<IResult> Make(HttpContext context, UserManager<NIdentityUser> users, UserSessionStore store)
    {
        var read = await JsonApiRequests.ReadAsync<UserSessionAttributes>(context.Request, USER_SESSIONS);
        if (read.Error != null)
        {
            return read.Error;
        }

        if (read.Id != null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "client-id-not-supported",
                "The id of a record is made by the server."
            );
        }

        var attributes = read.Attributes!;
        if (!Guid.TryParse(attributes.EventId, out var eventId) || eventId == Guid.Empty)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-event",
                "Name the Event the state is for by its id."
            );
        }

        if (attributes.State != null && !IsWhole(attributes.State))
        {
            return InvalidState();
        }

        var (session, created) = await store.MakeAsync(
            users.GetUserId(context.User)!,
            eventId,
            attributes.State,
            context.RequestAborted
        );
        return created
            ? ResourceOf(StatusCodes.Status201Created, session, $"/api/user-sessions/{session.Id}")
            : ResourceOf(StatusCodes.Status200OK, session);
    }

    static async Task<IResult> Change(
        HttpContext context,
        UserManager<NIdentityUser> users,
        UserSessionStore store,
        string id
    )
    {
        var read = await JsonApiRequests.ReadAsync<UserSessionAttributes>(context.Request, USER_SESSIONS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var owner = users.GetUserId(context.User)!;
        var session = Guid.TryParse(id, out var parsed)
            ? await store.FindAsync(owner, parsed, context.RequestAborted)
            : null;
        if (session is null)
        {
            return JsonApiResults.NotFound();
        }

        var attributes = read.Attributes!;
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

        if (attributes.State != null && !IsWhole(attributes.State))
        {
            return InvalidState();
        }

        if (attributes.State != null)
        {
            session = await store.ReplaceStateAsync(owner, session.Id, attributes.State, context.RequestAborted);
            if (session is null)
            {
                return JsonApiResults.NotFound(); // it was deleted while this request was on its way
            }
        }

        return ResourceOf(StatusCodes.Status200OK, session);
    }

    static async Task<IResult> Remove(
        HttpContext context,
        UserManager<NIdentityUser> users,
        UserSessionStore store,
        string id
    )
    {
        var removed =
            Guid.TryParse(id, out var parsed)
            && await store.DeleteAsync(users.GetUserId(context.User)!, parsed, context.RequestAborted);
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

    /// <summary>A state has both its lists, and no list and no entry in one is null: JSON can say that, the model cannot.</summary>
    static bool IsWhole(NtsUserSessionStateModel state)
    {
        return state.SnapshotHistory != null
            && state.SnapshotSelections != null
            && state.SnapshotHistory.All(group => group?.Entries != null && group.Entries.All(entry => entry != null))
            && state.SnapshotSelections.All(entry => entry != null);
    }

    static IResult InvalidState()
    {
        return JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "invalid-state",
            "The state is not valid.",
            "Send both lists, and no null in them or in the entries of a group."
        );
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
internal sealed class UserSessionAttributes
{
    /// <summary>A text and not a Guid, so that an id that is not one is refused with a code of its own.</summary>
    public string? EventId { get; set; }

    public NtsUserSessionStateModel? State { get; set; }
}
