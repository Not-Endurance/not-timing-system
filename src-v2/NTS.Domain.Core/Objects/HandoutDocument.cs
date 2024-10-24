using NTS.Domain.Core.Entities.ParticipationAggregate;
﻿using Not;
using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects;

public record HandoutDocument : Document, IIdentifiable
{
    public HandoutDocument(Handout handout, EnduranceEvent enduranceEvent, IEnumerable<Official> officials)
        : base(
            new DocumentHeader(handout.Participation.Competition.Name, enduranceEvent.PopulatedPlace, enduranceEvent.EventSpan, officials))
    {
        Id = handout.Id;
        Combination = handout.Participation.Combination;
        Phases = handout.Participation.Phases;
    }

    public int Id { get; }
    public Combination Combination { get; }
    public PhaseCollection Phases { get; }
}
