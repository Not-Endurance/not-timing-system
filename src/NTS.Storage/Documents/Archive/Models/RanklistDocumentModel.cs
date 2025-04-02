using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;

namespace NTS.Storage.Documents.Archive.Models;

public class RanklistDocumentModel
{
    public static RanklistDocumentModel Create(Ranklist ranklist)
    {
        return new RanklistDocumentModel
        {
            Name = ranklist.Name,
            Ruleset = ranklist.Ruleset,
            Type = ranklist.Type,
            Category = ranklist.Category,
            FeiRule = ranklist.Ranking.FeiRule,
            FeiEventCode = ranklist.Ranking.FeiEventCode,
            FeiScheduleNumber = ranklist.Ranking.FeiScheduleNumber,
            FeiCategoryEventNumber = ranklist.Ranking.FeiCategoryEventNumber,
            Entries = ranklist.Entries.Select(RankingEntryDocumentModel.Create).ToArray(),
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public AthleteCategory Category { get; init; }
    
    public string? FeiRule { get; init; }
    public string? FeiEventCode { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public string? FeiCategoryEventNumber { get; init; }
    public RankingEntryDocumentModel[] Entries { get; init; } = [];
}
