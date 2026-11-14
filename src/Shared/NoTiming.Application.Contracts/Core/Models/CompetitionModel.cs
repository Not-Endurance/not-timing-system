using NoTiming.Domain.Core.Aggregates.Participations.Objects;

namespace NoTiming.Application.Contracts.Core.Models;

public class CompetitionModel
{
    public static CompetitionModel MapFrom(Competition competition)
    {
        return new CompetitionModel { Name = competition.Name, EnglishNames = competition.EnglishNames };
    }

    public string Name { get; init; } = default!;
    public bool EnglishNames { get; init; }

    public Competition MapToEntity()
    {
        return new Competition(Name, EnglishNames);
    }
}
