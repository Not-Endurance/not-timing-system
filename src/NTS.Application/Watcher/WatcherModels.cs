using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Application.Watcher;

public class IntermediateSnapshotModel
{
    public static IntermediateSnapshotModel MapFrom(IntermediateSnapshot snapshot)
    {
        return new IntermediateSnapshotModel
        {
            Number = snapshot.Number,
            Names = snapshot.Athlete.Names,
            Timestamp = snapshot.Timestamp.ToString(),
        };
    }

    public int Number { get; set; }
    public string[] Names { get; set; } = [];
    public string Timestamp { get; set; } = "";

    public IntermediateSnapshot MapToDomain()
    {
        var athlete = new Person(Names);
        var timestamp = new Timestamp(Timestamp);
        return new IntermediateSnapshot(Number, athlete, timestamp);
    }
}

public class SnapshotModel
{
    public static SnapshotModel MapFrom(SnapshotPayload payload)
    {
        return new SnapshotModel
        {
            Entries = payload.Entries.AsEnumerable().Select(IntermediateSnapshotModel.MapFrom).ToArray(),
            Type = payload.Type.ToString(),
        };
    }

    public IntermediateSnapshotModel[] Entries { get; set; } = [];
    public string Type { get; set; } = "";

    public SnapshotPayload MapToDomain()
    {
        var snapshots = Entries.Select(entry => entry.MapToDomain());
        return new SnapshotPayload(snapshots, Type);
    }
}
