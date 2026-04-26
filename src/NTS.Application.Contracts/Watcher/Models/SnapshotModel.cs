using Not.Application.Authentication.User;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
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
            Names = snapshot.Athlete.Names,
            Timestamp = snapshot.Timestamp?.ToString(),
        };
    }

    public int Number { get; set; }
    public string[] Names { get; set; } = [];
    public string? Timestamp { get; set; } = "";

    public Snapshot MapToDomain()
    {
        var athlete = new Person(Names);
        var timestamp = Timestamp == null ? null : new Timestamp(Timestamp);
        return new Snapshot(Number, athlete, timestamp);
    }

    public SnapshotModel Copy()
    {
        return new SnapshotModel
        {
            Number = Number,
            Names = [.. Names],
            Timestamp = Timestamp,
        };
    }
}
