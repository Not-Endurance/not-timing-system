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
        // Do not assign ID, as in this case even if we want to map from existing
        // entry we want to copy the values and always perform Create on submit
        // Same goes for Name - we want to prompt the user to provide name.
        // TODO: above commends suggest this should be a separate flow
        Ruleset = ranking.Ruleset;
        Type = ranking.Type;
        Category = ranking.Category;
        CompetitionFeiId = ranking.CompetitionFeiId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        Entries = ranking.Entries.ToList();
    }
}
