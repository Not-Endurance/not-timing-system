using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Judge.Features.Core.Rankings;

public class CustomRankingModel
{
    public CustomRankingModel() { }

    public CustomRankingModel(Ranking ranking)
    {
        Name = ranking.Name;
        Ruleset = ranking.Ruleset;
        Type = ranking.Type;
        Category = ranking.Category;
        CompetitionFeiId = ranking.CompetitionFeiId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        Entries = ranking.Entries.ToList();
    }

    public string? Name { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public CompetitionType? Type { get; set; }
    public ParticipationCategory? Category { get; set; }
    public string? CompetitionFeiId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public List<RankingEntry> Entries { get; set; } = [];
}
