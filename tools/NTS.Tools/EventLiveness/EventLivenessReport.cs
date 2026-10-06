using MongoDB.Bson;

namespace NTS.Tools.EventLiveness;

/// <summary>What <c>migrate-event-liveness</c> found, and what it did or would do.</summary>
public sealed class EventLivenessReport
{
    public EventLivenessReport(bool apply, DateTimeOffset now)
    {
        Apply = apply;
        Now = now;
    }

    /// <summary>Whether the run was asked to persist (<c>--apply</c>) and not only to report.</summary>
    public bool Apply { get; }

    /// <summary>The instant the rule was applied at: an Event is Live while this is before the end of its last day.</summary>
    public DateTimeOffset Now { get; }

    /// <summary>Whether the apply ran. False for a dry run and for a run that refused.</summary>
    public bool Applied { get; internal set; }

    /// <summary>The documents of the Events collection.</summary>
    public int Seen { get; internal set; }

    /// <summary>The Events whose last day is ahead: Live under the rule.</summary>
    public int Live { get; internal set; }

    /// <summary>The Events whose last day has ended, or ends at this instant: Historic under the rule.</summary>
    public int Historic { get; internal set; }

    /// <summary>The Events that were deleted (soft-deleted): neither, and not asked about.</summary>
    public int Deleted { get; internal set; }

    /// <summary>The documents that store the flag.</summary>
    public int WithFlag { get; internal set; }

    /// <summary>The documents the flag was removed from.</summary>
    public int Removed { get; internal set; }

    /// <summary>The Events that were inactive and whose last day is still ahead, which are Live again under the rule.</summary>
    public List<RevivedEvent> Revived { get; } = [];

    /// <summary>The Events whose last day is not a date, so that the rule cannot be applied to them.</summary>
    public List<UnreadableLastDay> Unreadable { get; } = [];

    /// <summary>
    /// An apply does not run while an Event would become Live again, which the owner decides about first, or while the last
    /// day of an Event is not a date, which cannot be judged without guessing.
    /// </summary>
    public bool Refused => Apply && (Revived.Count > 0 || Unreadable.Count > 0);

    /// <summary>A Guid is written as one, an integer as itself.</summary>
    internal static string Describe(BsonValue value)
    {
        return value is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } binary
            ? binary.ToGuid(GuidRepresentation.Standard).ToString()
            : value.ToString() ?? "";
    }

    public void WriteTo(TextWriter writer)
    {
        writer.WriteLine(Apply ? "Applying the event-liveness migration." : "Dry-run event-liveness migration.");
        writer.WriteLine($"As of {Now.UtcDateTime:yyyy-MM-dd HH:mm:ss}Z");
        writer.WriteLine(
            $"{EventLivenessMigration.EVENTS}: {Seen} documents read: {Live} Live under the rule, {Historic} Historic, "
                + $"{Deleted} deleted"
        );
        writer.WriteLine(DescribeFlag());

        writer.WriteLine($"Events that are inactive and whose last day is still ahead: {Revived.Count}");
        foreach (var revived in Revived)
        {
            writer.WriteLine(
                $"  {EventLivenessMigration.EVENTS} {Describe(revived.Id)} \"{revived.Name}\": {revived.Flag}, "
                    + $"the last day ends {revived.EndsAt:yyyy-MM-dd HH:mm:ss}Z"
            );
        }

        writer.WriteLine($"Events whose EndDay is not a date: {Unreadable.Count}");
        foreach (var unreadable in Unreadable)
        {
            writer.WriteLine(
                $"  {EventLivenessMigration.EVENTS} {Describe(unreadable.Id)} \"{unreadable.Name}\": "
                    + (unreadable.Type?.ToString() ?? "not there")
            );
        }

        if (Refused)
        {
            writer.WriteLine(
                "Refusing to apply while an Event that was inactive is Live again under the rule (correct its EndDay or "
                    + "remove it, then run the dry run again) or an EndDay is not a date. Nothing was changed."
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

    string DescribeFlag()
    {
        if (WithFlag == 0)
        {
            return "IsActive is not stored on any Event: nothing to remove";
        }

        return Applied
            ? $"IsActive: removed from {Removed} documents"
            : $"IsActive: stored on {WithFlag} documents, to remove";
    }
}

/// <summary>An Event that was inactive and has a last day ahead, which makes it Live again under the rule.</summary>
public sealed class RevivedEvent
{
    public RevivedEvent(BsonValue id, string name, DateTime endsAt, string flag)
    {
        Id = id;
        Name = name;
        EndsAt = endsAt;
        Flag = flag;
    }

    public BsonValue Id { get; }
    public string Name { get; }

    /// <summary>The end of the last day, in UTC.</summary>
    public DateTime EndsAt { get; }

    /// <summary>What the document said, such as <c>IsActive false</c> or <c>IsActive not stored</c>.</summary>
    public string Flag { get; }
}

/// <summary>An Event whose <c>EndDay</c> is missing or is something other than a date.</summary>
public sealed class UnreadableLastDay
{
    public UnreadableLastDay(BsonValue id, string name, BsonType? type)
    {
        Id = id;
        Name = name;
        Type = type;
    }

    public BsonValue Id { get; }
    public string Name { get; }

    /// <summary>The type the value has, or none when the document has no <c>EndDay</c>.</summary>
    public BsonType? Type { get; }
}
