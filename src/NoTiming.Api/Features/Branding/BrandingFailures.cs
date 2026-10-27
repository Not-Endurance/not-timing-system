using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Branding;

/// <summary>
/// The answers of Branding that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class BrandingFailures
{
    public static JsonApiFailure UnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "Only the left and the right logo can be changed."
        );

    public static JsonApiFailure LogoNamedTwice { get; } =
        new(StatusCodes.Status400BadRequest, "malformed-request", "A logo is named once.");

    /// <summary>The detail says what is wrong with the logo.</summary>
    public static JsonApiFailure InvalidLogo { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-logo", "The logo cannot be kept.");

    public static JsonApiFailure LogoTooLarge { get; } =
        new(StatusCodes.Status422UnprocessableEntity, "logo-too-large", "The logo cannot be kept.");
}
