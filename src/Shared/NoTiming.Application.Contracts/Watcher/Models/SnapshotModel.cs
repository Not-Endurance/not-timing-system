using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;

namespace NoTiming.Application.Contracts.Watcher.Models;

public class SnapshotModel
{
    public static SnapshotModel MapFrom(Snapshot snapshot)
    {
        return new SnapshotModel
        {
            Number = snapshot.Number,
            Name = snapshot.Name,
            NameEnglish = snapshot.NameEnglish,
            EnglishNames = snapshot.EnglishNames,
            Timestamp = snapshot.Timestamp?.ToString(),
        };
    }

    public int Number { get; set; }
    public string Name { get; set; } = default!;
    public string? NameEnglish { get; set; }
    public bool EnglishNames { get; set; }
    public string? Timestamp { get; set; }

    public Snapshot MapToDomain()
    {
        var timestamp = string.IsNullOrWhiteSpace(Timestamp) ? null : new Timestamp(Timestamp);
        return new Snapshot(Number, Name, NameEnglish, timestamp, EnglishNames);
    }

    public SnapshotModel Copy()
    {
        return new SnapshotModel
        {
            Number = Number,
            Name = Name,
            NameEnglish = NameEnglish,
            EnglishNames = EnglishNames,
            Timestamp = Timestamp,
        };
    }
}
