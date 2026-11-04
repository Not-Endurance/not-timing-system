using NoTiming.Ui.Contracts.Features.Access;

namespace NoTiming.Ui.Blazor.Features;

public static class UiAccessPolicy
{
    public static bool CanViewSnapshots(UiAccessLevel accessLevel)
    {
        return accessLevel == UiAccessLevel.Official;
    }

    public static string ResolveHomeRoute(UiAccessLevel accessLevel)
    {
        return accessLevel == UiAccessLevel.Official ? Routes.SNAPSHOT_PAGE : Routes.PERFORMANCE_PAGE;
    }

    public static bool ShouldRedirectFromSnapshots(UiAccessLevel accessLevel)
    {
        return accessLevel == UiAccessLevel.Participant;
    }

    public static string ResolveSnapshotFallbackRoute()
    {
        return Routes.PERFORMANCE_PAGE;
    }
}
