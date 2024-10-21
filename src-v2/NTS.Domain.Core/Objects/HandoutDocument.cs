using NTS.Domain.Core.Entities.ParticipationAggregate;
﻿using Not;
using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects;

public record HandoutDocument : Document, IIdentifiable
{
    public HandoutDocument(Participation participation, EnduranceEvent enduranceEvent, IEnumerable<Official> officials)
        : base(new DocumentHeader(participation.Competition.Name, enduranceEvent.PopulatedPlace, enduranceEvent.EventSpan, officials))
    {
        Id = participation.Id;
        Tandem = participation.Tandem;
        Phases = participation.Phases;
    }

    public int Id { get; }
    public Tandem Tandem { get; }
    public PhaseCollection Phases { get; }
}
