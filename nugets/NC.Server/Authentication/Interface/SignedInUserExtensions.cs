using System.Security.Claims;

namespace NC.Server.Authentication.Interface;

/// <summary>Reads the id of the signed-in user from the principal that the session scheme sets on the request.</summary>
public static class SignedInUserExtensions
{
    public static int? GetSignedInUserId(this ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
