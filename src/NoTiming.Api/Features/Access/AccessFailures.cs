using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Access;

/// <summary>
/// The answers of Access that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class AccessFailures
{
    public static JsonApiFailure RequestHeaderRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "request-header-required",
            "Send the header the pages send with every write.",
            $"{AccessBaseline.WRITE_HEADER}: {AccessBaseline.WRITE_HEADER_VALUE}"
        );

    public static JsonApiFailure InvalidFilter { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter is not valid.",
            "Name the Event: filter=eventId eq <id>."
        );

    public static JsonApiFailure EventRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "event-required",
            "Name the Event the request is about.",
            "The Event is the eventId attribute of the document."
        );

    public static JsonApiFailure InvalidEvent { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-event",
            "The Event is not named.",
            "Name the Event by its id as eventId."
        );

    public static JsonApiFailure NoCurrentTenant { get; } =
        new(
            StatusCodes.Status409Conflict,
            "no-current-tenant",
            "Choose a country on your profile before you do this: it is done in the Tenant of one."
        );

    public static JsonApiFailure SetupIdRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "id-required",
            "Name the Setup the Event starts from.",
            "The id of the resource is the id of its Setup."
        );

    public static JsonApiFailure EventIdInvalid { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-id",
            "The Event is not named.",
            "The id of the resource is the id of the Event, and it is a Guid."
        );

    public static JsonApiFailure NotSignedIn { get; } =
        new(StatusCodes.Status401Unauthorized, "not-signed-in", "Sign in to do this.");

    public static JsonApiFailure NotAllowed { get; } =
        new(StatusCodes.Status403Forbidden, "not-allowed", "You may not do this.");

    public static JsonApiFailure NotRoot { get; } =
        new(
            StatusCodes.Status403Forbidden,
            "not-root",
            "Only Root may do this: a Tenant Root of the Tenant, or the Developer."
        );

    public static JsonApiFailure NotConsoleOperator { get; } =
        new(
            StatusCodes.Status403Forbidden,
            "not-console-operator",
            "Only a Console Operator of the Event, or Root, may do this."
        );

    public static JsonApiFailure NotDeveloper { get; } =
        new(StatusCodes.Status403Forbidden, "not-developer", "Only the Developer may do this.");

    public static JsonApiFailure TenantNotOperational { get; } =
        new(
            StatusCodes.Status409Conflict,
            "tenant-not-operational",
            "The Tenant has no Tenant Root yet, so it cannot hold Events."
        );

    public static JsonApiFailure EventNotStarted { get; } =
        new(StatusCodes.Status409Conflict, "event-not-started", "The Event has not started.");

    public static JsonApiFailure EventStarted { get; } =
        new(
            StatusCodes.Status409Conflict,
            "event-started",
            "The Event has started: this can only be done before it does."
        );

    public static JsonApiFailure EventEnded { get; } =
        new(StatusCodes.Status409Conflict, "event-ended", "The Event has ended, and nothing about it can be changed.");
}
