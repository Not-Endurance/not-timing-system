using NTS.Warp.ACL.Enums;

namespace NTS.Warp.Features.Witness.ProcessSnapshots;

public class ProcessSnapshotsPayload
{
    public IEnumerable<EmsSnapshotModel> Entries { get; init; } = [];
    public EmsWitnessEventType Type { get; init; }
}

public class EmsSnapshotModel
{
    public int LapNumber { get; init; }
    public string Number { get; init; } = default!;
    public string Name { get; init; } = default!;
    public DateTimeOffset ArriveTime { get; set; }
    public double LapDistance { get; set; }
}
