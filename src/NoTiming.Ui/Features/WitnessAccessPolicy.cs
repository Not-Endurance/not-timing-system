using NTS.Contracts.Features.Access;

namespace NoTiming.Ui.Features;

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

    public static bool CanSignIn(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Anonymous;
    }
}
