﻿using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Entities;

public class Handout : DomainEntity
{
    private Handout()
    {
    }
    public Handout(Participation participation)
    {
        Competition = participation.Competition;
        Tandem = participation.Tandem;
        Phases = participation.Phases;
    }

    public string Competition { get; private set; } = default!;
    public Tandem Tandem { get; private set; } = default!;
    public PhaseCollection Phases { get; private set; } = default!;
}
