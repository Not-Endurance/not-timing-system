using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The answers of EventData that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class EventDataFailures
{
    public static JsonApiFailure EventRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "event-required",
            "Name the Event the rows are of.",
            "Start the filter with the Event and join anything else to it with and: filter=eventId eq <id> and ..."
        );

    public static JsonApiFailure MalformedRequest { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "The request is malformed.",
            "The version is a whole number, from 0."
        );

    public static JsonApiFailure VersionRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "version-required",
            "Say which version of the Participation the change is based on.",
            "Send the version it was read at as the version of the meta of the document."
        );

    public static JsonApiFailure ParticipationInUse { get; } =
        new(
            StatusCodes.Status409Conflict,
            "participation-in-use",
            "A Ranking still counts the Participation, or a Handout is still of it.",
            "Take it out of the Rankings that count it and remove the Handouts that are of it first: what they show could not be composed without it."
        );

    public static JsonApiFailure ParticipationChanged { get; } =
        new(
            StatusCodes.Status409Conflict,
            "participation-changed",
            "The Participation changed since it was read.",
            "Read it again and make the change on what it is now."
        );

    public static JsonApiFailure EventDataMalformedRequest { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "The request is malformed.",
            "The Event is named once, as an id."
        );
}
