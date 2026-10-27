using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Events;

/// <summary>
/// The answers of Events that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class EventsFailures
{
    /// <summary>A Setup that is not whole cannot start an Event: what is missing is the detail.</summary>
    public static JsonApiFailure InvalidSetup(string title, string detail)
    {
        return new JsonApiFailure(StatusCodes.Status422UnprocessableEntity, "invalid-setup", title, detail);
    }

    public static JsonApiFailure TenantNotFound { get; } =
        new(StatusCodes.Status409Conflict, "tenant-not-found", "The Event belongs to no Tenant that can start it.");

    public static JsonApiFailure InvalidAttribute { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "invalid-attribute",
            "A member of the resource is not valid.",
            "An Event is over when its last day is, and is not ended by changing its days."
        );

    public static JsonApiFailure EventInvalidAttribute { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "invalid-attribute",
            "A member of the resource is not valid.",
            "An Event does not start after its last day."
        );

    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "An Event is made with a name, a location and optionally an FEI show ID, and with nothing else."
        );

    public static JsonApiFailure InvalidName { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-name",
            "An Event needs a name of one line of at most 100 characters."
        );

    public static JsonApiFailure InvalidLocation { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-location",
            "An Event needs a location of one line of at most 100 characters."
        );

    public static JsonApiFailure InvalidFeiShowId { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-fei-show-id",
            "An FEI show ID is at most 20 characters of one line."
        );

    public static JsonApiFailure EventUnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "An Event is started from its Setup and takes nothing else."
        );

    public static JsonApiFailure AccountNotFound { get; } =
        new(StatusCodes.Status422UnprocessableEntity, "account-not-found", "There is no such account.");

    public static JsonApiFailure EventGrantUnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "A grant names an Event, a kind, a role for an Official, and a person by an email or an account."
        );

    public static JsonApiFailure ConsoleOperatorUnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "A Console Operator names an Event and a person by an email or an account."
        );

    public static JsonApiFailure MalformedRequest { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "Name the person by an email or by an account, and by one of them."
        );

    public static JsonApiFailure InvalidEmail { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-email", "Enter a valid email address.");

    public static JsonApiFailure IncompleteFeiConfiguration { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "incomplete-fei-configuration",
            "The FEI export is only partly configured."
        );

    public static JsonApiFailure InvalidKind { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-kind", "A grant is for an operator or an official.");

    public static JsonApiFailure InvalidOfficialRole { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-role", "An official has a role of the domain, such as Steward.");

    public static JsonApiFailure OperatorHasNoRole { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-role", "An operator has no role.");

    public static JsonApiFailure GrantLimitReached { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "grant-limit-reached",
            $"An Event has at most {EventGrantStore.MAX_GRANTS_PER_EVENT} grants: remove one that is not needed."
        );

    public static JsonApiFailure TenantCountryMissing { get; } =
        new(
            StatusCodes.Status409Conflict,
            "tenant-country-missing",
            "The country of the Tenant is not among the countries, so no Event can be made in it."
        );
}
