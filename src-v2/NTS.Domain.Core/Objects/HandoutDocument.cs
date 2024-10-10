using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;

namespace NTS.Domain.Core.Objects;

public record HandoutDocument : DomainObject
{
    public HandoutDocument(Handout handout, Participation participation, Event enduranceEvent, IEnumerable<Official> officials)
    {
        Header = new DocumentHeader(participation.Competition.Name, enduranceEvent.PopulatedPlace, enduranceEvent.EventSpan, officials);
        Tandem = participation.Tandem;
        Phases = participation.Phases;
        HandoutId = handout.Id;
    }

    public int HandoutId { get; }
    public DocumentHeader Header { get; }
    public Tandem Tandem { get; }
    public PhaseCollection Phases { get; }
}
