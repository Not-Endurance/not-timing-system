using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.EnduranceEvents.Models;

public class RanklistModel
{
    public RanklistModel(Ranklist ranklist)
    {
        Name = ranklist.Name;
        Ruleset = ranklist.Ruleset;
        Type = ranklist.Type;
        Category = ranklist.Category;
        Entries = ranklist.Select(e => new RankingEntryModel(e)).ToArray();
    }

    public string Name { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public AthleteCategory Category { get; init; }
    public RankingEntryModel[] Entries { get; init; }
}
