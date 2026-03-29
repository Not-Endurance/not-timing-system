using System.Security.Claims;

namespace Not.Application.Authentication.User;

public static class NScopeClaimsHelper
{
    static readonly string[] SCOPE_CLAIMS =
    [
        "scp",
        "scope",
        "http://schemas.microsoft.com/identity/claims/scope",
    ];

    public static bool HasScope(ClaimsPrincipal principal, string? requiredScope)
    {
        if (string.IsNullOrWhiteSpace(requiredScope))
        {
            return true;
        }

        return principal
            .Claims
            .Where(x => SCOPE_CLAIMS.Contains(x.Type, StringComparer.OrdinalIgnoreCase))
            .SelectMany(x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(x => string.Equals(x, requiredScope, StringComparison.OrdinalIgnoreCase));
    }
}
