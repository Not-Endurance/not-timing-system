using Not.Application.Authentication.User;
using NTS.Witness.Contracts.Features.Profile;

namespace NTS.Witness.Blazor.Features.Profile;

public static class WitnessProfileRoutePolicy
{
    public static bool ShouldRedirectToProfile(NUserModel? user, string relativePath)
    {
        return user != null && !WitnessProfilePolicy.IsComplete(user) && IsProfileGatedRoute(relativePath);
    }

    /// <summary>
    /// Only Snapshotting is gated on a complete profile. Gating the read-only routes would leave a
    /// signed-in user with less access than an anonymous visitor.
    /// </summary>
    public static bool IsProfileGatedRoute(string relativePath)
    {
        return string.Equals(Normalize(relativePath), Routes.SNAPSHOT_PAGE, StringComparison.OrdinalIgnoreCase);
    }

    static string Normalize(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Split('?', '#')[0].Trim('/');
        return string.IsNullOrWhiteSpace(path) ? "/" : $"/{path}";
    }
}
