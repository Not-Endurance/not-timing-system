using Not.Application.Authentication.User;
using Not.Blazor.Client.Authentication;
using NTS.Witness.Contracts.Features.Profile;

namespace NTS.Witness.Blazor.Features.Profile;

public static class WitnessProfileRoutePolicy
{
    public static bool ShouldRedirectToProfile(NUserModel? user, string relativePath)
    {
        return user != null
            && !WitnessProfilePolicy.IsComplete(user)
            && !IsProfileGateBypassRoute(relativePath);
    }

    public static bool IsProfileGateBypassRoute(string relativePath)
    {
        var path = Normalize(relativePath);
        return string.Equals(path, Routes.PROFILE_PAGE, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"/{AuthenticationContents.AUTHENTICATION}", StringComparison.OrdinalIgnoreCase);
    }

    static string Normalize(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Split('?', '#')[0].Trim('/');
        return string.IsNullOrWhiteSpace(path) ? "/" : $"/{path}";
    }
}
