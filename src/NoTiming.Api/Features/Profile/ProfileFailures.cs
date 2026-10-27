using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Profile;

/// <summary>
/// The answers of Profile that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class ProfileFailures
{
    public static JsonApiFailure ProfileIdMismatch { get; } =
        new(
            StatusCodes.Status409Conflict,
            "profile-id-mismatch",
            "The profile is the caller's own: the id of the resource is not theirs."
        );

    public static JsonApiFailure ProfileIncomplete { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "profile-incomplete",
            "A profile needs a country, a first name and a surname."
        );

    public static JsonApiFailure InvalidName { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-name", "A name is at most 100 characters of one line.");

    public static JsonApiFailure InvalidClub { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-club", "A club is at most 100 characters of one line.");

    public static JsonApiFailure InvalidFeiId { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-fei-id", "An FEI ID is at most 20 characters of one line.");

    public static JsonApiFailure InvalidCountry { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-country", "Choose a country from the list.");
}
