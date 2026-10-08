using NTS.Contracts.Features.Account;

namespace NoTiming.Ui.Features.Account;

/// <summary>
/// What a page asks of the person (#645, ADR-0001, ADR-0012). Everything that only shows is public, so a visitor has all of
/// it; the pages that send Snapshots and the profile are for a person who is signed in; and only sending Snapshots waits for
/// a complete profile, as gating the read-only pages would leave a person who is signed in with less than a visitor. The
/// host decides what a person may do whatever the page shows, so this is what is offered and not what is allowed.
/// </summary>
public static class WitnessRoutePolicy
{
    public static RouteAccess AccessTo(CurrentAccount? account, string relativePath)
    {
        if (!IsSnapshotPage(relativePath) && !IsRouteOf(Routes.PROFILE_PAGE, relativePath))
        {
            return RouteAccess.Open;
        }

        if (account == null)
        {
            return RouteAccess.SignIn;
        }

        return !account.ProfileComplete && IsSnapshotPage(relativePath) ? RouteAccess.Profile : RouteAccess.Open;
    }

    static bool IsSnapshotPage(string relativePath)
    {
        return IsRouteOf(Routes.SNAPSHOT_PAGE, relativePath) || IsRouteOf(Routes.EVENT_SNAPSHOT_PAGE, relativePath);
    }

    /// <summary>
    /// Whether the address is the route, whatever the case, the slashes at the ends, the query or the fragment. A segment of
    /// the route in braces is the id of an Event, which an address that names anything else is not the route of.
    /// </summary>
    static bool IsRouteOf(string route, string relativePath)
    {
        var wanted = Segments(route);
        var given = Segments(relativePath);
        return wanted.Length == given.Length
            && wanted.Zip(given).All(segment => IsMatch(segment.First, segment.Second));
    }

    static bool IsMatch(string wanted, string given)
    {
        return wanted.StartsWith('{')
            ? Guid.TryParse(given, out _)
            : string.Equals(wanted, given, StringComparison.OrdinalIgnoreCase);
    }

    static string[] Segments(string path)
    {
        return (path ?? string.Empty).Split('?', '#')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
    }
}
