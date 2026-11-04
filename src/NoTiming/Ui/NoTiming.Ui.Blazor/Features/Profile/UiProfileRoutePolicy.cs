using NC.Application.Authentication.User;
using NC.Blazor.Client.Authentication;
using NoTiming.Ui.Contracts.Features.Profile;

namespace NoTiming.Ui.Blazor.Features.Profile;

public static class UiProfileRoutePolicy
{
    public static bool ShouldRedirectToProfile(NUserModel? user, string relativePath)
    {
        return user != null && !UiProfilePolicy.IsComplete(user) && !IsProfileGateBypassRoute(relativePath);
    }

    public static bool IsProfileGateBypassRoute(string relativePath)
    {
        var path = Normalize(relativePath);
        return string.Equals(path, Normalize(Routes.PROFILE_PAGE), StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"/{AuthenticationContents.AUTHENTICATION}", StringComparison.OrdinalIgnoreCase);
    }

    static string Normalize(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Split('?', '#')[0].Trim('/');
        return string.IsNullOrWhiteSpace(path) ? "/" : $"/{path}";
    }
}
