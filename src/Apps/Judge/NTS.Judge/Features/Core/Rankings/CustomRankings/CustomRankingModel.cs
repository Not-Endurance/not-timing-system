using Not.Krud.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Judge.Features.Core.Rankings.CustomRankings;

public record CustomRankingModel : KrudFormModel<Ranking>
{
    public CustomRankingModel() { }

    public CustomRankingModel(Ranking ranking)
    {
        MapFrom(ranking);
    }

    public string? Name { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public CompetitionType? Type { get; set; }
    public ParticipationCategory? Category { get; set; }
    public string? CompetitionFeiId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }
    public List<RankingEntry> Entries { get; set; } = [];

    protected override Ranking MapTo()
    {
        return new Ranking(Name, Ruleset, Type, Category, CompetitionFeiId, FeiRule, FeiScheduleNumber, Entries, Id);
    }

    public override void MapFrom(Ranking ranking)
    {
        Id = ranking.Id;
        Name = ranking.Name;
        Ruleset = ranking.Ruleset;
        Type = ranking.Type;
        Category = ranking.Category;
        CompetitionFeiId = ranking.CompetitionFeiId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        Entries = ranking.Entries.ToList();
    }
}
