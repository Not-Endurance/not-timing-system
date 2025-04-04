using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Archive.Models;

public class CompetitionDocumentModel
{
    public static CompetitionDocumentModel Create(Competition competition)
    {
        return new CompetitionDocumentModel
        {
            Name = competition.Name,
            Ruleset = competition.Ruleset,
            Type = competition.Type,
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }

    public Competition ToDomain()
    {
        return new Competition(Name, Ruleset, Type);
    }
}
