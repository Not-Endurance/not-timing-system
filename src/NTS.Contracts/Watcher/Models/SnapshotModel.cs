using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Contracts.Watcher.Models;

public class SnapshotModel
{
    public static SnapshotModel MapFrom(Snapshot snapshot)
    {
        return new SnapshotModel
        {
            Number = snapshot.Number,
            Name = snapshot.Name,
            NameEnglish = snapshot.NameEnglish,
            Ruleset = snapshot.Ruleset,
            Timestamp = snapshot.Timestamp?.ToString(),
            Time = snapshot.Timestamp?.ToDateTimeOffset(),
        };
    }

    public int Number { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public string? Timestamp { get; set; }

    /// <summary>
    /// The instant of the time, with its date, which the text of the time of the day does not say (#618): what an Update of a
    /// Snapshot that was sent needs, as it names an instant. None in what was kept before it was kept.
    /// </summary>
    public DateTimeOffset? Time { get; set; }

    public Snapshot MapToDomain()
    {
        var timestamp =
            Time is { } instant ? new Timestamp(instant)
            : string.IsNullOrWhiteSpace(Timestamp) ? null
            : new Timestamp(Timestamp);
        return new Snapshot(Number, Name, NameEnglish, timestamp, Ruleset);
    }

    public SnapshotModel Copy()
    {
        return new SnapshotModel
        {
            Number = Number,
            Name = Name,
            NameEnglish = NameEnglish,
            Ruleset = Ruleset,
            Timestamp = Timestamp,
            Time = Time,
        };
    }
}
