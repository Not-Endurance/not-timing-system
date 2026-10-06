namespace NTS.Domain.Access;

/// <summary>
/// What the Core views show of an Event and whether they may change it, by the stage the Event is in (#630, ADR-0007).
/// A Live Event shows every Core view. A Historic Event is a record: it shows its Rankings and Results and the
/// Participation detail with the times that were recorded, and hides the views that are about the running Event (the
/// Startlist, Handouts, the Arrivelist and Presentlist, Snapshot capture and Performance). Whether a view may write is
/// the stage and the permission together: the Api decides the permission (<see cref="AccessPolicy"/>), and a Historic
/// Event takes no write from a view whoever looks at it.
/// </summary>
public static class EventViewPolicy
{
    public static bool Shows(CoreView view, EventStage stage)
    {
        return view switch
        {
            CoreView.Rankings or CoreView.Results or CoreView.ParticipationDetail => stage != EventStage.Unstarted,
            CoreView.Startlist
            or CoreView.Handouts
            or CoreView.Arrivelist
            or CoreView.Presentlist
            or CoreView.SnapshotCapture
            or CoreView.Performance => stage == EventStage.Live,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "A Core view the policy has no row for."),
        };
    }

    public static bool CanWrite(EventStage stage, bool permitted)
    {
        return stage == EventStage.Live && permitted;
    }
}
