using NTS.ACL.Entities;
using NTS.ACL.Entities.EMS;
using NTS.Domain;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.ACL.Factories;

public class SnapshotFactory
{
    public static Snapshot Create(EmsParticipantEntry participant, EmsWitnessEventType emsType, bool isFinal)
    {
        var number = int.Parse(participant.Number);
        var method = SnapshotMethod.EmsIntegration;
        var timestamp = new Timestamp(participant.ArriveTime!.Value);

        return emsType switch
        {
            EmsWitnessEventType.VetIn => new Snapshot(number, SnapshotType.Vet, method, timestamp),
            EmsWitnessEventType.Arrival => isFinal
                ? new Snapshot(number, SnapshotType.Final, method, timestamp)
                : new Snapshot(number, SnapshotType.Stage, method, timestamp),
            _ => throw new Exception($"Invalid WitnessEventType for participant '{participant.Number}'"),
        };
    }
}
