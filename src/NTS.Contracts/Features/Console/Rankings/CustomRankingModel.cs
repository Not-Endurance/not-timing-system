using Not.Krud.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Contracts.Features.Console.Rankings;

/// <summary>
/// A Ranking that the Console Operator makes of Participations it chooses, over the settings of one that exists (#646): it is
/// always made new, and a Ranking that was copied from keeps its settings but not its name or its id.
/// </summary>
public record CustomRankingModel : KrudFormModel<Ranking>
{
    public CustomRankingModel() { }

    public CustomRankingModel(Ranking ranking)
    {
        MapFrom(ranking);
    }

    public string? Name { get; set; }
    public CompetitionRuleset? Ruleset { get; set; }
    public ParticipationCategory? Category { get; set; }
    public string? FeiEventId { get; set; }
    public string? FeiEventCode { get; set; }
    public string? FeiCompetitionId { get; set; }
    public string? FeiRule { get; set; }
    public string? FeiScheduleNumber { get; set; }

    /// <summary>The Event the Ranking is made for: the one the page shows, set by the service that makes it.</summary>
    public Guid EventId { get; set; }

    public List<CustomRankingEntryModel> Entries { get; set; } = [];

    protected override Ranking MapTo()
    {
        return new Ranking(
            Name,
            Ruleset,
            Category,
            FeiEventId,
            FeiEventCode,
            FeiCompetitionId,
            FeiRule,
            FeiScheduleNumber,
            Entries
                .Where(x => x.Participation != null)
                .Select(x => new RankingEntry(x.Participation!.Id, x.IsNotRanked)),
            EventId,
            Id
        );
    }

    public override void MapFrom(Ranking ranking)
    {
        // The id and the name are not copied: whoever copies a Ranking makes another one, and names it.
        Ruleset = ranking.Ruleset;
        Category = ranking.Category;
        FeiEventId = ranking.FeiEventId;
        FeiEventCode = ranking.FeiEventCode;
        FeiCompetitionId = ranking.FeiCompetitionId;
        FeiRule = ranking.FeiRule;
        FeiScheduleNumber = ranking.FeiScheduleNumber;
        EventId = ranking.EventId;
    }
}
