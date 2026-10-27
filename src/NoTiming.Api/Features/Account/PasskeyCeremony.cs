using Microsoft.AspNetCore.Antiforgery;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// What every request of a passkey ceremony is asked before its document is looked at, in the order of its protocol (ADR-0002,
/// ADR-0012): the host supports passkeys, and the antiforgery token that goes with the page is good. The routes that make the
/// options, enrol a passkey and sign in with one ask it the same way.
/// </summary>
internal static class PasskeyCeremony
{
    /// <summary>The answer that refuses the request, or none when the ceremony may go on.</summary>
    public static async Task<IResult?> RefuseAsync(
        HttpContext context,
        PasskeyService passkeys,
        IAntiforgery antiforgery
    )
    {
        if (!passkeys.Availability.Enabled)
        {
            return JsonApiResults.Error(
                StatusCodes.Status503ServiceUnavailable,
                "passkeys-not-configured",
                "Passkeys are not available on this host."
            );
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "antiforgery-token-invalid",
                "The request could not be verified. Reload the page and try again."
            );
        }
    }
}
