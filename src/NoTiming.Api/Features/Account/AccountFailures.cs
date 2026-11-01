using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The answers of Account that clients branch on (ADR-0008): the status, the stable code and what a person reads, said
/// once, so that no handler holds a code string and a test names the code it expects.
/// </summary>
internal static class AccountFailures
{
    /// <summary>The name of a passkey is at most this long.</summary>
    public static JsonApiFailure InvalidPasskeyName(int maxLength)
    {
        return new JsonApiFailure(
            StatusCodes.Status400BadRequest,
            "invalid-name",
            "The name is too long.",
            $"Use at most {maxLength} characters."
        );
    }

    public static JsonApiFailure InvalidEmail { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-email", "Enter a valid email address.");

    public static JsonApiFailure SessionInvalidPasskey { get; } =
        new(StatusCodes.Status401Unauthorized, "invalid-passkey", "The passkey could not sign you in.");

    public static JsonApiFailure InvalidCode { get; } =
        new(StatusCodes.Status401Unauthorized, "invalid-code", "The code is wrong or has expired.");

    public static JsonApiFailure AccountIdMismatch { get; } =
        new(
            StatusCodes.Status409Conflict,
            "account-id-mismatch",
            "The account is the caller's own: the id of the resource is not theirs."
        );

    public static JsonApiFailure NotAMember { get; } =
        new(
            StatusCodes.Status422UnprocessableEntity,
            "not-a-member",
            "An account selects only a Tenant it is a member of."
        );

    public static JsonApiFailure MeUnsupportedAttribute { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "unsupported-attribute",
            "Only the selected Tenant of an account can be changed."
        );

    public static JsonApiFailure MeMalformedRequest { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "malformed-request",
            "The selected Tenant is the id of a Tenant, or null to go back to the home Tenant."
        );

    public static JsonApiFailure PasskeyNotVerified { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-passkey", "The passkey could not be verified.");

    public static JsonApiFailure PasskeyRenameConflict { get; } =
        new(StatusCodes.Status409Conflict, "conflict", "The passkey could not be renamed.");

    public static JsonApiFailure LastPasskey { get; } =
        new(
            StatusCodes.Status409Conflict,
            "last-passkey",
            "The last passkey cannot be removed.",
            "Add another passkey first."
        );

    public static JsonApiFailure PasskeyRemoveConflict { get; } =
        new(StatusCodes.Status409Conflict, "conflict", "The passkey could not be removed.");

    public static JsonApiFailure PasskeysNotConfigured { get; } =
        new(
            StatusCodes.Status503ServiceUnavailable,
            "passkeys-not-configured",
            "Passkeys are not available on this host."
        );

    public static JsonApiFailure AntiforgeryTokenInvalid { get; } =
        new(
            StatusCodes.Status400BadRequest,
            "antiforgery-token-invalid",
            "The request could not be verified. Reload the page and try again."
        );

    public static JsonApiFailure RegistrationInvalidName { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-name", "Enter your first name and your surname.");

    public static JsonApiFailure RegistrationInvalidCountry { get; } =
        new(StatusCodes.Status400BadRequest, "invalid-country", "Choose a country from the list.");
}
