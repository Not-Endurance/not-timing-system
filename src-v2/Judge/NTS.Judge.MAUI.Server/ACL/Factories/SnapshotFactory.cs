﻿using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Judge.MAUI.Server.ACL.EMS;

namespace NTS.Judge.MAUI.Server.ACL.Factories;

public class SnapshotFactory
{
    public static Snapshot Create(EmsParticipantEntry participant, EmsWitnessEventType emsType, bool isFinal)
    {
        var number = int.Parse(participant.Number);
        var type = emsType switch
        {
            EmsWitnessEventType.VetIn => SnapshotType.Vet,
            EmsWitnessEventType.Arrival => isFinal
                ? SnapshotType.Final
                : SnapshotType.Stage,
            _ => throw new Exception($"Invalid WitnessEventType for participant '{participant.Number}'"),
        };

        return new Snapshot(number, type, SnapshotMethod.EmsIntegration);
    }
}