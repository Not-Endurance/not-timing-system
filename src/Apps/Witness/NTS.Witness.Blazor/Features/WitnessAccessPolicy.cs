using NTS.Witness.Contracts.Features.Access;

namespace NTS.Witness.Blazor.Features;

public static class WitnessAccessPolicy
{
    public static bool CanViewSnapshots(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Official;
    }

    public static string ResolveHomeRoute(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Official ? Routes.SNAPSHOT_PAGE : Routes.PERFORMANCE_PAGE;
    }

    /// <summary>
    /// Write access is only knowable once an event is connected — until then every signed-in user
    /// reads as <see cref="WitnessAccessLevel.Registered"/>. Redirecting on that would bounce an
    /// Official off a deep link to /snapshot before the socket ever connects.
    /// </summary>
    public static bool ShouldRedirectFromSnapshots(WitnessAccessLevel accessLevel, bool isEventConnected)
    {
        return isEventConnected && accessLevel is WitnessAccessLevel.Anonymous or WitnessAccessLevel.Registered;
    }

    public static bool CanSignIn(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Anonymous;
    }

    public static string ResolveSnapshotFallbackRoute()
    {
        return Routes.PERFORMANCE_PAGE;
    }
}
