using NoTiming.Ui.Contracts.Features.Access;

namespace NoTiming.Ui.Blazor.Features;

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

    public static bool ShouldRedirectFromSnapshots(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Participant;
    }

    public static string ResolveSnapshotFallbackRoute()
    {
        return Routes.PERFORMANCE_PAGE;
    }
}
