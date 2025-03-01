using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class RankingModel
{
    public RankingModel(Ranking domainModel)
    {
        Name = domainModel.Name;
        Ruleset = domainModel.Ruleset;
        Type = domainModel.Type;
        Category = domainModel.Category;
        Entries = domainModel.Entries.Select(e => new RankingEntryModel(e)).ToArray();
    }

    public string Name { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public AthleteCategory Category { get; init; }
    public RankingEntryModel[] Entries { get; init; }
}
