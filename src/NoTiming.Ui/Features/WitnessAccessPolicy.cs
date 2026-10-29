using NTS.Contracts.Features.Access;

namespace NoTiming.Ui.Features;

public static class WitnessAccessPolicy
{
    public static bool CanViewSnapshots(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Official;
    }

    /// <summary>
    /// The page the home address leads to, in the Live Event the app follows: the Snapshot page for a person who may send
    /// Snapshots to it and the Performance page for anybody else. With no Live Event there is no page of one to lead to,
    /// and the person stays at the home address.
    /// </summary>
    public static string ResolveHomeRoute(WitnessAccessLevel accessLevel, Guid? liveEventId)
    {
        if (liveEventId is not { } eventId)
        {
            return Routes.HOME;
        }

        var page =
            accessLevel == WitnessAccessLevel.Official ? Routes.EVENT_SNAPSHOT_PAGE : Routes.EVENT_PERFORMANCE_PAGE;
        return Routes.Of(page, eventId);
    }

    public static bool CanSignIn(WitnessAccessLevel accessLevel)
    {
        return accessLevel == WitnessAccessLevel.Anonymous;
    }
}
