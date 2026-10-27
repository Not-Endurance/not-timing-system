using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// The answers of Reference that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class ReferenceFailures
{
    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "The document names a member that is not one of the resource's, or one that the server alone sets."
        );

    public static JsonApiFailure MalformedRequest { get; } =
        new(StatusCodes.Status400BadRequest, "malformed-request", "The request is malformed.");

    public static JsonApiFailure InvalidAttribute { get; } =
        new(StatusCodes.Status422UnprocessableEntity, "invalid-attribute", "A member of the resource is not valid.");

    public static JsonApiFailure MissingMember { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "invalid-attribute",
            "A member of the resource is not valid.",
            "The document is missing a member it needs."
        );

    public static JsonApiFailure InvalidSortList { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-sort",
            "Sort by members, with a - for descending: sort=-name,id."
        );

    public static JsonApiFailure EmptyFilter { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-filter", "The filter is empty.");

    public static JsonApiFailure InvalidFilter { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-filter",
            "The filter is not valid.",
            "Use an expression of the OData filter grammar over the members of the resource."
        );

    public static JsonApiFailure InvalidSort { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-sort",
            "The sort is not valid.",
            "Sort by the members of the resource, with a - for descending: sort=-name,id."
        );
}
