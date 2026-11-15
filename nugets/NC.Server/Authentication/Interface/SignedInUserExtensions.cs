using System.Security.Claims;

namespace NC.Server.Authentication.Interface;

/// <summary>Reads the id of the signed-in user from the principal that the session scheme sets on the request.</summary>
public static class SignedInUserExtensions
{
    public static int? GetSignedInUserId(this ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    /// <summary>
    /// The id of the signed-in user, for an endpoint the SIGNED_IN policy guards. The policy admits only a principal that
    /// carries the id, so the id is never missing there.
    /// </summary>
    public static int GetRequiredSignedInUserId(this ClaimsPrincipal principal)
    {
        return principal.GetSignedInUserId()
            ?? throw new InvalidOperationException("The SIGNED_IN policy admitted a principal without a user id.");
    }
}
