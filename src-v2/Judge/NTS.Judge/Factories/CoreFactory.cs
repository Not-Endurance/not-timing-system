using Not.Application.Ports.CRUD;
using Not.Domain;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using NTS.Domain.Enums;

namespace NTS.Judge.Factories;

public class CoreFactory
{
    public static EnduranceEvent CreateEvent (Domain.Setup.Entities.EnduranceEvent setupEvent)
    {
        if (!setupEvent.Competitions.Any())
        {
            throw new DomainException("Cannot start - Competitions aren't configured");
        }
        var competitionStartTimes = setupEvent.Competitions.Select(x => x.Start);
        var startDate = competitionStartTimes.First();
        var endDate = competitionStartTimes.Last();

        // TODO: fix city and pla
        var enduranceEvent = new EnduranceEvent(setupEvent.Country, setupEvent.Place, "", startDate, endDate, null, null, null);
        return enduranceEvent;
    }

    public static Official CreateOfficial(Domain.Setup.Entities.Official official)
    {
        var coreOfficial = new Official(official.Person, official.Role);
        return coreOfficial;
    }

    public static async Task<(List<Participation> Participations, Dictionary<AthleteCategory, List<RankingEntry>> RankingEntriesByCategory)>
        CreateParticipationAndRankingEntriesAsync(Domain.Setup.Entities.Competition setupCompetition, IRepository<Participation> participationRepository)
    {
        if (setupCompetition.Phases.Count == 0)
        {
            throw new DomainException($"Cannot start - Phases of competition {setupCompetition.Name} aren't configured");
        }
        if (setupCompetition.Participations.Count == 0)
        {
            throw new DomainException($"Cannot start - Particiaptions of competition {setupCompetition.Name} aren't configured");
        }

        var competitionDistance = 0m;
        var participations = new List<Participation>();
        var rankingEntriesByCategory = new Dictionary<AthleteCategory, List<RankingEntry>>
        {
            { AthleteCategory.Senior, new List<RankingEntry>() },
            { AthleteCategory.Children, new List<RankingEntry>() },
            { AthleteCategory.JuniorOrYoungAdult, new List<RankingEntry>() },
            { AthleteCategory.Training, new List<RankingEntry>() }
        };
        foreach (var contestant in setupCompetition.Participations)
        {
            DateTimeOffset? startTime = setupCompetition.Start;
            var setupPhases = setupCompetition.Phases;
            var phases = new List<Phase>();
            foreach (var phase in setupPhases)
            {
                var corePhase = new Phase(
                    phase.Loop!.Distance,
                    phase.Recovery,
                    phase.Rest,
                    setupCompetition.Ruleset,
                    setupPhases.Last() == phase,
                    setupCompetition.CompulsoryThresholdSpan,
                    startTime);
                startTime = null; //Set only first phase StartTime
                phases.Add(corePhase);
                competitionDistance += (decimal)phase.Loop!.Distance;
            }
            var setupCombination = contestant.Combination;
            var combination = new Combination(
                setupCombination.Number,
                setupCombination.Athlete.Person, 
                setupCombination.Horse.Name,
                competitionDistance,
                setupCombination.Athlete.Country,
                setupCombination.Athlete.Club,
                contestant.MinAverageSpeed,
                contestant.MaxAverageSpeed);
            var participation = new Participation(setupCompetition.Name, setupCompetition.Ruleset, combination, phases);
            var storedParticipations = await participationRepository.ReadAll();
            if(!storedParticipations.Any(p => p.Combination.Number == participation.Combination.Number))
            {               
                participations.Add(participation);
                var rankingEntry = new RankingEntry(participation, contestant.IsNotRanked);
                rankingEntriesByCategory[setupCombination.Athlete.Category].Add(rankingEntry);
            }
            else
            {
                var participationRef = storedParticipations.ToList().Find(p => p.Combination.Number == participation.Combination.Number);
                if(participationRef != null)
                {
                    var rankingEntry = new RankingEntry(participationRef, contestant.IsNotRanked);
                    rankingEntriesByCategory[setupCombination.Athlete.Category].Add(rankingEntry);
                }
            }
        }
        return (participations, rankingEntriesByCategory);
    }

    public static Ranking CreateRanking(Competition competition, AthleteCategory athleteCategory, IEnumerable<RankingEntry> rankingEntries)
    {
        var ranking = new Ranking(competition, athleteCategory, rankingEntries);
        return ranking;
    }
}
