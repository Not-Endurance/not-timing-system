using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.UserSessions;

/// <summary>
/// The answers of UserSession that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class UserSessionFailures
{
    public static JsonApiFailure EventImmutable { get; } =
        new(
            StatusCodes.Status409Conflict,
            "event-immutable",
            "The Event of a record does not change: make a record for the other Event."
        );

    public static JsonApiFailure InvalidFilter { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter is not valid.",
            "Use eventId eq <the id of an Event>."
        );

    public static JsonApiFailure InvalidState { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-state",
            "The state is not valid.",
            "Send both lists, and no null in them or in the entries of a group."
        );

    public static JsonApiFailure InvalidEvent { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-event", "Name the Event the state is for by its id.");
}
