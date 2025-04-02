using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Judge.Core.Start.Factories;

public class RankingFactory
{
    public static Ranking Create(
        Competition competition,
        AthleteCategory athleteCategory,
        IEnumerable<RankingEntry> rankingEntries,
        string? feiRule,
        string? feiEventCode,
        string? feiScheduleNumber,
        string? feiCategoryEventNumber
    )
    {
        var ranking = new Ranking(competition, athleteCategory, feiRule, feiEventCode, feiScheduleNumber, feiCategoryEventNumber, rankingEntries);
        return ranking;
    }
}
