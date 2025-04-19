using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.Features.Witness.ProcessSnapshots;

public class ProcessSnapshotsPayload
{
    public IEnumerable<EmsParticipantEntry> Entries { get; init; } = [];
    public EmsWitnessEventType Type { get; init; }
}
