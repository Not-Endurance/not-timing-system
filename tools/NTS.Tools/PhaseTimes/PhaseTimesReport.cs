using MongoDB.Bson;

namespace NTS.Tools.PhaseTimes;

/// <summary>What <c>migrate-phase-times</c> found, and what it did or would do.</summary>
public sealed class PhaseTimesReport
{
    public PhaseTimesReport(bool apply)
    {
        Apply = apply;
    }

    /// <summary>Whether the run was asked to persist (<c>--apply</c>) and not only to report.</summary>
    public bool Apply { get; }

    /// <summary>Whether anything was written. False for a dry run and for a run that refused.</summary>
    public bool Applied { get; internal set; }

    public ParticipationCounts Participations { get; internal set; } = new(0, 0, 0);

    /// <summary>The documents of the snapshot-results collection, which is dropped on apply; zero when it is not there.</summary>
    public int SnapshotResults { get; internal set; }

    public bool SnapshotResultsArePresent { get; internal set; }
    public bool SnapshotResultsDropped { get; internal set; }

    /// <summary>The flat times that are something other than a date.</summary>
    public List<UnreadableTime> Unreadable { get; } = [];

    /// <summary>The Rankings and Handouts that still hold a copy of a Participation.</summary>
    public List<StoredCopy> Copies { get; } = [];

    /// <summary>
    /// An apply does not run while a flat time is not a date, which cannot be turned into an event without guessing, or
    /// while a copy of a Participation is still stored, which means <c>migrate-participation-copies</c> has not run.
    /// </summary>
    public bool Refused => Apply && (Unreadable.Count > 0 || Copies.Count > 0);

    /// <summary>A Guid is written as one, an integer as itself.</summary>
    internal static string Describe(BsonValue value)
    {
        return value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            ? binary.ToGuid(GuidRepresentation.Standard).ToString()
            : value.ToString() ?? "";
    }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine(Apply ? "Applying the phase-times migration." : "Dry-run phase-times migration.");
        writer.WriteLine(Participations.Describe(Applied));
        writer.WriteLine(DescribeSnapshotResults());

        writer.WriteLine($"Times that are not a date: {Unreadable.Count}");
        foreach (var unreadable in Unreadable)
        {
            writer.WriteLine(
                $"  {PhaseTimesMigration.PARTICIPATIONS} {Describe(unreadable.DocumentId)} {unreadable.Where}: {unreadable.Type}"
            );
        }

        writer.WriteLine($"Rankings and Handouts that still hold a copy of a Participation: {Copies.Count}");
        foreach (var copy in Copies)
        {
            writer.WriteLine($"  {copy.Collection} {Describe(copy.DocumentId)}");
        }

        if (Refused)
        {
            writer.WriteLine(
                "Refusing to apply while a time is not a date or a copy of a Participation is still stored "
                    + "(run migrate-participation-copies first). Nothing was changed."
            );
        }
        else if (Applied)
        {
            writer.WriteLine("Done.");
        }
        else
        {
            writer.WriteLine("Nothing was changed. Run again with --apply to persist.");
        }
    }

    string DescribeSnapshotResults()
    {
        if (!SnapshotResultsArePresent)
        {
            return $"{PhaseTimesMigration.SNAPSHOT_RESULTS}: not there";
        }

        return $"{PhaseTimesMigration.SNAPSHOT_RESULTS}: {SnapshotResults} documents, "
            + (SnapshotResultsDropped ? "dropped" : "to drop");
    }
}

/// <summary>What the migration does to the Participations: documents read, documents it changes, events it makes.</summary>
public sealed class ParticipationCounts
{
    public ParticipationCounts(int documents, int changed, int events)
    {
        Documents = documents;
        Changed = changed;
        Events = events;
    }

    public int Documents { get; }
    public int Changed { get; }
    public int Events { get; }

    internal string Describe(bool applied)
    {
        var verb = applied ? "changed" : "to change";
        return $"{PhaseTimesMigration.PARTICIPATIONS}: {Documents} documents read, {Changed} {verb} ({Events} events)";
    }
}

/// <summary>A flat time of a Phase that is not a date, so that no event can be made of it.</summary>
public sealed class UnreadableTime
{
    public UnreadableTime(BsonValue documentId, string where, BsonType type)
    {
        DocumentId = documentId;
        Where = where;
        Type = type;
    }

    public BsonValue DocumentId { get; }

    /// <summary>The place in the document, such as <c>Phases[0].PresentTime</c>.</summary>
    public string Where { get; }

    public BsonType Type { get; }
}

/// <summary>A Ranking or a Handout that still holds the copy of a Participation that ADR-0006 replaced with its id.</summary>
public sealed class StoredCopy
{
    public StoredCopy(string collection, BsonValue documentId)
    {
        Collection = collection;
        DocumentId = documentId;
    }

    public string Collection { get; }
    public BsonValue DocumentId { get; }
}
