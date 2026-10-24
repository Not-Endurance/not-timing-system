using MongoDB.Bson;

namespace NTS.Tools.ParticipationCopies;

/// <summary>What <c>migrate-participation-copies</c> found, and what it did or would do.</summary>
public sealed class ParticipationCopiesReport
{
    public ParticipationCopiesReport(bool apply)
    {
        Apply = apply;
    }

    /// <summary>Whether the run was asked to persist (<c>--apply</c>) and not only to report.</summary>
    public bool Apply { get; }

    /// <summary>Whether anything was written. False for a dry run and for a run that refused.</summary>
    public bool Applied { get; internal set; }

    public List<CollectionCounts> Collections { get; } = [];
    public List<MissingParticipation> Missing { get; } = [];
    public List<DifferingCopy> Differing { get; } = [];
    public List<RepeatedParticipation> Repeated { get; } = [];

    /// <summary>
    /// An apply does not run while a Participation is missing, which would leave a reference to nothing, or is listed
    /// twice in a Ranking, which the application does not load.
    /// </summary>
    public bool Refused => Apply && (Missing.Count > 0 || Repeated.Count > 0);

    /// <summary>A Guid is written as one, an integer as itself.</summary>
    internal static string Describe(BsonValue value)
    {
        return value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            ? binary.ToGuid(GuidRepresentation.Standard).ToString()
            : value.ToString() ?? "";
    }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine(
            Apply ? "Applying the participation-copies migration." : "Dry-run participation-copies migration."
        );
        foreach (var counts in Collections)
        {
            writer.WriteLine(counts.Describe(Applied));
        }

        writer.WriteLine($"Participations missing: {Missing.Count}");
        foreach (var missing in Missing)
        {
            var participation =
                missing.ParticipationId == null ? "no Participation" : Describe(missing.ParticipationId);
            writer.WriteLine(
                $"  {missing.Collection} {Describe(missing.DocumentId)} {missing.Where}: {participation} ({missing.Reason})"
            );
        }

        writer.WriteLine($"Participations listed twice in one Ranking: {Repeated.Count}");
        foreach (var repeated in Repeated)
        {
            writer.WriteLine(
                $"  {repeated.Collection} {Describe(repeated.DocumentId)}: {Describe(repeated.ParticipationId)}"
            );
        }

        writer.WriteLine($"Entries whose copy differs from the stored Participation: {Differing.Count}");
        foreach (var differing in Differing)
        {
            writer.WriteLine(
                $"  {differing.Collection} {Describe(differing.DocumentId)} {differing.Where}: "
                    + $"{Describe(differing.ParticipationId)}: {string.Join(", ", differing.Fields)}"
            );
        }

        if (Refused)
        {
            writer.WriteLine(
                "Refusing to apply while a Participation is missing or listed twice in a Ranking. Nothing was changed."
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
}

/// <summary>What the migration does to one collection: documents read, documents it changes, entries it converts.</summary>
public sealed class CollectionCounts
{
    public CollectionCounts(string collection, int documents, int changed, int entries)
    {
        Collection = collection;
        Documents = documents;
        Changed = changed;
        Entries = entries;
    }

    public string Collection { get; }
    public int Documents { get; }
    public int Changed { get; }

    /// <summary>The entries of Rankings that are converted; zero for the other collections.</summary>
    public int Entries { get; }

    internal string Describe(bool applied)
    {
        var verb = applied ? "changed" : "to change";
        var entries = Entries > 0 ? $" ({Entries} entries)" : "";
        return $"{Collection}: {Documents} documents read, {Changed} {verb}{entries}";
    }
}

/// <summary>
/// An entry or a Handout that names a Participation which is not there: it names none, it is not in the Participations
/// collection, or it belongs to another Event.
/// </summary>
public sealed class MissingParticipation
{
    public MissingParticipation(
        string collection,
        BsonValue documentId,
        string where,
        BsonValue? participationId,
        string reason
    )
    {
        Collection = collection;
        DocumentId = documentId;
        Where = where;
        ParticipationId = participationId;
        Reason = reason;
    }

    public string Collection { get; }
    public BsonValue DocumentId { get; }

    /// <summary>The place in the document: <c>Entries[2]</c> of a Ranking, <c>Participation</c> of a Handout.</summary>
    public string Where { get; }

    public BsonValue? ParticipationId { get; }
    public string Reason { get; }
}

/// <summary>An embedded copy that is not what the stored Participation is now, and the fields that differ.</summary>
public sealed class DifferingCopy
{
    public DifferingCopy(
        string collection,
        BsonValue documentId,
        string where,
        BsonValue participationId,
        IReadOnlyList<string> fields
    )
    {
        Collection = collection;
        DocumentId = documentId;
        Where = where;
        ParticipationId = participationId;
        Fields = fields;
    }

    public string Collection { get; }
    public BsonValue DocumentId { get; }
    public string Where { get; }
    public BsonValue ParticipationId { get; }

    /// <summary>Paths in the Participation document, such as <c>Phases[0].PresentTime</c>.</summary>
    public IReadOnlyList<string> Fields { get; }
}

/// <summary>A Ranking that lists the same Participation in more than one entry.</summary>
public sealed class RepeatedParticipation
{
    public RepeatedParticipation(string collection, BsonValue documentId, BsonValue participationId)
    {
        Collection = collection;
        DocumentId = documentId;
        ParticipationId = participationId;
    }

    public string Collection { get; }
    public BsonValue DocumentId { get; }
    public BsonValue ParticipationId { get; }
}
