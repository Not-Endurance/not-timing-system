using MongoDB.Bson;

namespace NTS.Tools.ParticipationCopies;

/// <summary>
/// What the domain derives from the recorded times and the code before ADR-0006 stored beside them: the Total of a
/// Participation and nine values of each of its Phases.
/// </summary>
internal static class DerivedValues
{
    const string TOTAL = "Total";
    const string PHASES = "Phases";
    static readonly string[] PHASE_FIELDS =
    [
        "RequiredInspectionTime",
        "OutTime",
        "LoopInterval",
        "PhaseInterval",
        "RecoveryInterval",
        "AverageLoopSpeed",
        "AveragePhaseSpeed",
        "AverageSpeed",
        "IsComplete",
    ];

    /// <summary>Whether the Participation document carries its Total or a derived value of a Phase.</summary>
    public static bool AreIn(BsonDocument participation)
    {
        return participation.Contains(TOTAL) || Phases(participation).Any(x => PHASE_FIELDS.Any(x.Contains));
    }

    /// <summary>The document as a new one without them. The order of what is left is kept.</summary>
    public static BsonDocument Without(BsonDocument participation)
    {
        var stripped = participation.DeepClone().AsBsonDocument;
        stripped.Remove(TOTAL);
        foreach (var phase in Phases(stripped))
        {
            foreach (var field in PHASE_FIELDS)
            {
                phase.Remove(field);
            }
        }

        return stripped;
    }

    static IEnumerable<BsonDocument> Phases(BsonDocument participation)
    {
        return participation.TryGetValue(PHASES, out var phases) && phases is BsonArray array
            ? array.OfType<BsonDocument>()
            : [];
    }
}
