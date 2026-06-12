using NTS.Domain.Core.Aggregates.Results;

namespace NTS.Domain.Core.Aggregates;

public sealed class Handout : Result
{
    public Handout(Participation participation, int? id = null)
        : base(
            id,
            null,
            participation.Competition.Name,
            participation.Competition.Ruleset,
            participation.Category,
            [new ParticipationResult(participation)],
            participation.EventId
        ) { }
}
