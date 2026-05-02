using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;

namespace NTS.Application.Contracts.Core.Models;

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel
        {
            Name = competition.Name,
            Ruleset = competition.Ruleset,
            Type = competition.Type,
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionType Type { get; init; }
    public CompetitionRuleset Ruleset { get; init; }

    public Competition MapToEntity()
    {
        return new Competition(Name, Ruleset, Type);
    }
}
