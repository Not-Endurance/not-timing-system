namespace NoTiming.Api.JsonApi;

/// <summary>
/// The answers of JsonApi that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class JsonApiFailures
{
    /// <summary>A page is a number of at least 1 and a size of 1 to the greatest the list takes.</summary>
    public static JsonApiFailure InvalidPage(int maxSize)
    {
        return new JsonApiFailure(
            StatusCodes.Status400BadRequest,
            "invalid-page",
            $"A page is a number of at least 1 and a size of 1 to {maxSize}."
        );
    }

    public static JsonApiFailure InvalidId { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-id", "The id of a resource is a Guid.");

    public static JsonApiFailure IdMismatch { get; } =
        new(StatusCodes.Status409Conflict, "id-mismatch", "The id of the resource is not the one of the route.");

    public static JsonApiFailure IdForbidden { get; } =
        new(StatusCodes.Status403Forbidden, "client-id-not-supported", "The id of a record is made by the server.");

    public static JsonApiFailure IdRequired { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "id-required",
            "The resource needs the id a client makes for it.",
            "Send a Guid as the id of the resource."
        );

    public static JsonApiFailure InvalidFilter { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-filter", "The filter is not valid.");

    public static JsonApiFailure UnsupportedParameter { get; } =
        new(StatusCodes.Status400BadRequest, "unsupported-parameter", "The parameter is not supported.");

    public static JsonApiFailure InvalidSort { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-sort", "The sort is not valid.");

    public static JsonApiFailure UnsupportedMediaType { get; } =
        new(StatusCodes.Status415UnsupportedMediaType, "unsupported-media-type", $"Send {JsonApiResults.MEDIA_TYPE}.");

    public static JsonApiFailure PayloadTooLarge { get; } =
        new(
            StatusCodes.Status413PayloadTooLarge,
            "payload-too-large",
            "The body is too large.",
            $"Send at most {JsonApiRequests.MAX_BODY_BYTES} bytes."
        );

    public static JsonApiFailure MalformedRequest { get; } =
        new(StatusCodes.Status400BadRequest, "malformed-request", "The request is malformed.");

    /// <summary>A filter that is valid in the grammar and that the data cannot run: the date parts of a date, say.</summary>
    public static JsonApiFailure ExpressionNotSupported { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter cannot be applied.",
            "The expression is valid, but the data cannot run it: write the comparison on the member itself."
        );

    /// <summary>A filter that is valid and that the database cannot run on a row: a division by zero, a part of a text that starts past its end.</summary>
    public static JsonApiFailure ExpressionUnrunnable { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter cannot be applied.",
            "The expression is valid, but the database cannot run it on every row: look at what it divides by, or takes a part of."
        );

    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "The document names a member that is not one of the resource's, or one that the server alone sets."
        );

    public static JsonApiFailure NotFound { get; } = new(StatusCodes.Status404NotFound, "not-found", "Not found");

    public static JsonApiFailure NotSignedIn { get; } =
        new(StatusCodes.Status401Unauthorized, "not-signed-in", "Sign in to do this.");
}
