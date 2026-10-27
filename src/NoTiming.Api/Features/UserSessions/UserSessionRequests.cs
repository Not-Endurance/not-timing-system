using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Watcher.Models;

namespace NoTiming.Api.Features.UserSessions;

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
