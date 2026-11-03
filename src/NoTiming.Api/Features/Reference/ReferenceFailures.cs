using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Reference;

/// <summary>
/// The answers of Reference that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class ReferenceFailures
{
    public static JsonApiFailure InvalidAttribute { get; } =
        new(StatusCodes.Status422UnprocessableEntity, "invalid-attribute", "A member of the resource is not valid.");

    public static JsonApiFailure MissingMember { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "invalid-attribute",
            "A member of the resource is not valid.",
            "The document is missing a member it needs."
        );

    public static JsonApiFailure InvalidSortMembers { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "invalid-sort",
            "Sort by members, with a - for descending: sort=-name,id."
        );

    public static JsonApiFailure EmptyFilter { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-filter", "The filter is empty.");
}
