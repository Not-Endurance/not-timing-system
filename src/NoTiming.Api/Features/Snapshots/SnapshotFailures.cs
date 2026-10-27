using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Snapshots;

/// <summary>
/// The answers of Snapshot that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class SnapshotFailures
{
    public static JsonApiFailure MalformedRequest { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "The request is malformed.",
            $"Send each Snapshot as a resource of type '{SnapshotDocuments.TYPE}' with attributes."
        );

    public static JsonApiFailure EventMismatch { get; } =
        new(
            StatusCodes.Status409Conflict,
            "event-mismatch",
            "The Snapshot is of another Event than the group.",
            "A group is sent to one Event: send the Snapshots of another in a group of their own."
        );

    public static JsonApiFailure TooManySnapshots { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "too-many-snapshots",
            "A group has too many Snapshots.",
            $"Send at most {SnapshotDocuments.MAX_IN_A_GROUP} in one request."
        );

    public static JsonApiFailure ParticipationNotFound { get; } =
        new(
            StatusCodes.Status404NotFound,
            "participation-not-found",
            "The Event has no Participation with that start number."
        );

    public static JsonApiFailure IdTaken { get; } =
        new(
            StatusCodes.Status409Conflict,
            "id-taken",
            "An event with that id was recorded for another Participation, or is not a Snapshot."
        );

    public static JsonApiFailure NotFound { get; } = new(StatusCodes.Status404NotFound, "not-found", "Not found");

    public static JsonApiFailure ParticipationBusy { get; } =
        new(
            StatusCodes.Status409Conflict,
            "participation-busy",
            "The Participation was being changed by too many at once.",
            "Send it again: a Snapshot is recorded once whatever the number of times it is sent."
        );

    public static JsonApiFailure EventRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "event-required",
            "Name the Event the Snapshot is of.",
            "The Event is the eventId attribute of the document."
        );

    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "The document names a member that is not one of the resource's, or one that the server alone sets."
        );

    public static JsonApiFailure InvalidSnapshot { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-snapshot", "The document is not a Snapshot.");

    public static JsonApiFailure IdRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "id-required",
            "A Snapshot has the id its device made for it.",
            "Send a Guid as the id of the resource: it is how a Snapshot sent again is told from a new one."
        );
}
