using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
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
            CompetitionFeiId = ranklist.Ranking.CompetitionFeiId,
            FeiRule = ranklist.Ranking.FeiRule,
            FeiScheduleNumber = ranklist.Ranking.FeiScheduleNumber,
            Entries = ranklist.Entries.Select(RankingEntryDocumentModel.Create).ToArray(),
        };
    }

    public string Name { get; init; } = default!;
    public CompetitionRuleset Ruleset { get; init; }
    public CompetitionType Type { get; init; }
    public AthleteCategory Category { get; init; }
    public string? CompetitionFeiId { get; init; }
    public string? FeiRule { get; init; }
    public string? FeiScheduleNumber { get; init; }
    public RankingEntryDocumentModel[] Entries { get; init; } = [];

    public Ranklist ToDomain()
    {
        var entries = Entries.Select(x => x.ToDomain());
        var competition = new Competition(Name, Ruleset, Type);
        var ranking = new Ranking(competition, Category, CompetitionFeiId, FeiRule, FeiScheduleNumber, entries);
        return new Ranklist(ranking, entries);
    }
}
