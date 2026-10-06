namespace NTS.Domain.Access;

/// <summary>
/// A view of the Core that shows an Event (#630, ADR-0007). Every Core view takes its Event from the route, and whether
/// the Event shows it is decided by <see cref="EventViewPolicy"/> from the stage the Event is in.
/// </summary>
public enum CoreView
{
    Startlist = 1,
    Rankings = 2,

    /// <summary>The Results of a Ranking: composed from the stored Participations (ADR-0006), printable.</summary>
    Results = 3,

    /// <summary>One Participation with its recorded times, and nothing derived from a clock.</summary>
    ParticipationDetail = 4,
    Handouts = 5,
    Arrivelist = 6,
    Presentlist = 7,

    /// <summary>The page on which Staff capture and send Snapshots.</summary>
    SnapshotCapture = 8,

    /// <summary>The live view of how a Participation is doing, which follows the running Event.</summary>
    Performance = 9,
}
