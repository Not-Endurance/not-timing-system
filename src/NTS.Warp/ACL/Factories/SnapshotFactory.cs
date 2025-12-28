using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Warp.ACL.Entities;
using NTS.Warp.ACL.Enums;
using NTS.Warp.Features.Witness.ProcessSnapshots;

namespace NTS.Warp.ACL.Factories;

public class SnapshotFactory
{
    public static Snapshot Create(EmsSnapshotModel participant, EmsWitnessEventType emsType)
    {
        var number = int.Parse(participant.Number);
        var method = SnapshotMethod.EmsIntegration;
        var timestamp = new Timestamp(participant.ArriveTime);

        return emsType switch
        {
            EmsWitnessEventType.VetIn => new Snapshot(number, SnapshotType.Vet, method, timestamp),
            EmsWitnessEventType.Arrival => new Snapshot(number, SnapshotType.Stage, method, timestamp),
            _ => throw new Exception($"Invalid WitnessEventType for participant '{participant.Number}'"),
        };
    }
}
