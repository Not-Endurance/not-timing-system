using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Application.Contracts.Watcher.Models;

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
        };
    }

    public int Number { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public string? Timestamp { get; set; }

    public Snapshot MapToDomain()
    {
        var timestamp = string.IsNullOrWhiteSpace(Timestamp) ? null : new Timestamp(Timestamp);
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
        };
    }
}
