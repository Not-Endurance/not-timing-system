using Not.Domain;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Enums;
using Phase = NTS.Domain.Core.Aggregates.Participations.Phase;
using Event = NTS.Domain.Core.Entities.Event;
using Official = NTS.Domain.Core.Entities.Official;
using Competition = NTS.Domain.Core.Aggregates.Participations.Competition;

namespace NTS.Judge.Factories;
public class CoreFactory
{
    public static Event CreateEvent (Domain.Setup.Entities.Event setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            throw new DomainException("Cannot start - Competitions aren't configured");
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.StartTime);
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        var @event = new Event(setupEvent.Country, setupEvent.Place, "", startDate, endDate, null, null, null); // TODO: fix city and pla
        return @event;
    }

    public static Official CreateOfficial(Domain.Setup.Entities.Official official)
    {
        var coreOfficial = new Official(official.Person, official.Role);
        return coreOfficial;
    }

    public static (List<Participation> Participations, Dictionary<AthleteCategory, List<RankingEntry>> RankingEntriesByCategory) CreateParticipationAndRankingEntries(Domain.Setup.Entities.Competition setupCompetition)
    {
        if (setupCompetition.Phases.Count == 0)
        {
            throw new DomainException($"Cannot start - Phases of competition {setupCompetition.Name} aren't configured");
        }
        if ( setupCompetition.Contestants.Count == 0)
        {
            throw new DomainException($"Cannot start - Contestants of competition {setupCompetition.Name} aren't configured");
        }
        var setupPhases = setupCompetition.Phases;
        var competitionDistance = 0m;
        var phases = new List<Phase>();
        var participations = new List<Participation>();
        var rankingEntriesByCategory = new Dictionary<AthleteCategory, List<RankingEntry>>
        {
            { AthleteCategory.Senior, new List<RankingEntry>() },
            { AthleteCategory.Children, new List<RankingEntry>() },
            { AthleteCategory.JuniorOrYoungAdult, new List<RankingEntry>() },
            { AthleteCategory.Training, new List<RankingEntry>() }
        };
        foreach (var phase in setupPhases)
        {
            var corePhase = new Phase(phase.Loop!.Distance, phase.Recovery, phase.Rest, setupCompetition.Ruleset, setupPhases.Last() == phase, setupCompetition.CriRecovery);
            phases.Add(corePhase);
            competitionDistance += (decimal)phase.Loop!.Distance;
        }
        foreach (var contestant in setupCompetition.Contestants)
        {
            var combination = contestant.Combination;
            var tandem = new Tandem(combination.Number, combination.Athlete.Person, combination.Horse.Name, competitionDistance, combination.Athlete.Country, combination.Athlete.Club, combination.Athlete.Category, setupCompetition.Type, contestant.MaxSpeedOverride);
            var participation = new Participation(setupCompetition.Name, setupCompetition.Ruleset, tandem, phases);
            participations.Add(participation);
            var rankingEntry = new RankingEntry(participation, !contestant.IsUnranked);
            rankingEntriesByCategory[combination.Athlete.Category].Add(rankingEntry);
        }
        return (participations, rankingEntriesByCategory);
    }

    public static Ranking CreateRanking(Competition competition, AthleteCategory athleteCategory, IEnumerable<RankingEntry> rankingEntries)
    {
        var ranking = new Ranking(competition, athleteCategory, rankingEntries);
        return ranking;
    }
}
