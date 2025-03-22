using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Archive.Models;

public class RanklistDocumentModel
{
    public RanklistDocumentModel(Ranklist ranklist)
    {
        Name = ranklist.Name;
        Ruleset = ranklist.Ruleset;
        Type = ranklist.Type;
        Category = ranklist.Category;
        Entries = ranklist.Entries.Select(e => new RankingEntryDocumentModel(e)).ToArray();
    }

    public string Name { get; init; }
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public AthleteCategory Category { get; init; }
    public RankingEntryDocumentModel[] Entries { get; init; }
}
