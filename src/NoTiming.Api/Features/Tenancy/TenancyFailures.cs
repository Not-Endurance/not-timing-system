using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The answers of Tenancy that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class TenancyFailures
{
    public static JsonApiFailure SearchTooShort { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "search-too-short",
            $"Search with a word of at least {NameSearch.MIN_WORD_LENGTH} characters."
        );

    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "Only the rules of the Regional competitions of a Tenant can be changed."
        );

    public static JsonApiFailure InvalidRules { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-rules",
            "The rules are onlyAverageLoopSpeed (true or false) and rankerCode (a short text, or null for none)."
        );

    public static JsonApiFailure TenantIdMismatch { get; } =
        new(StatusCodes.Status409Conflict, "tenant-id-mismatch", "The id of the resource is not the one of the route.");
}
