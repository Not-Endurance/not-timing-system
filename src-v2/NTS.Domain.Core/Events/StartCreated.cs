﻿using Not.Events;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Events;

public record StartCreated : DomainObject
{
    #pragma warning disable CS8618 // Deserialization ctor
    private StartCreated() {}
    #pragma warning restore CS8618
    public StartCreated(Participation participation)
    {
        if (participation.IsNotQualified)
        {
            throw GuardHelper.Exception($"{participation} cannot start");
        }
        if (participation.Phases.OutTime == null)
        {
            throw GuardHelper.Exception($"{participation} cannot start because current OutTime is null");
        }

        Number = participation.Tandem.Number;
        Athlete = participation.Tandem.Name;
        LoopNumber = participation.Phases.CurrentNumber;
        Distance = participation.Phases.Distance;
        StartAt = participation.Phases.OutTime;
    }

    public int Number { get; private set; }
    public Person Athlete { get; private set; }
    public int LoopNumber { get; private set; }
    public double Distance { get; private set; }
    public Timestamp StartAt { get; private set; }
}
