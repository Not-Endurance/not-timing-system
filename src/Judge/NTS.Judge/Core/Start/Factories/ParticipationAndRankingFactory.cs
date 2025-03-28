using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Judge.Core.Start.Factories;

public static class ParticipationAndRankingrFactory
{
    public static async Task<(
        List<Participation> Participations,
        Dictionary<AthleteCategory, List<RankingEntry>> RankingEntriesByCategory
    )> Create(Domain.Setup.Aggregates.Competition setupCompetition, IRepository<Participation> participationRepository)
    {
        if (setupCompetition.Phases.Count == 0)
        {
            throw new DomainException(
                $"Cannot start - Phases of competition {setupCompetition.Name} aren't configured"
            );
        }
        if (setupCompetition.Participations.Count == 0)
        {
            throw new DomainException(
                $"Cannot start - Particiaptions of competition {setupCompetition.Name} aren't configured"
            );
        }

        var competitionDistance = 0m;
        var participations = new List<Participation>();
        var rankingEntriesByCategory = new Dictionary<AthleteCategory, List<RankingEntry>>();
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
                    startTime
                );
                startTime = null; //Set only first phase StartTime
                phases.Add(corePhase);
                competitionDistance += (decimal)phase.Loop!.Distance;
            }
            var setupCombination = contestant.Combination;
            var combination = new Combination(
                setupCombination.Number,
                setupCombination.Athlete,
                setupCombination.Horse,
                competitionDistance,
                setupCombination.Athlete.Country,
                setupCombination.Athlete.Club,
                contestant.MinAverageSpeed,
                contestant.MaxAverageSpeed
            );
            var participation = new Participation(
                setupCompetition.Name,
                setupCompetition.Ruleset,
                setupCompetition.Type,
                combination,
                phases
            );
            var storedParticipations = await participationRepository.ReadAll();
            if (!storedParticipations.Any(p => p.Combination.Number == participation.Combination.Number))
            {
                participations.Add(participation);
                var rankingEntry = new RankingEntry(participation, contestant.IsNotRanked);
                AddRanking(rankingEntriesByCategory, setupCombination.Athlete.Category, rankingEntry);
            }
            else
            {
                var participationRef = storedParticipations
                    .ToList()
                    .Find(p => p.Combination.Number == participation.Combination.Number);
                if (participationRef != null)
                {
                    var rankingEntry = new RankingEntry(participationRef, contestant.IsNotRanked);
                    AddRanking(rankingEntriesByCategory, setupCombination.Athlete.Category, rankingEntry);
                }
            }
        }
        return (participations, rankingEntriesByCategory);
    }

    static void AddRanking(Dictionary<AthleteCategory, List<RankingEntry>> dictionary, AthleteCategory category, RankingEntry entry)
    {
        if (dictionary.TryGetValue(category, out List<RankingEntry>? value))
        {
            value.Add(entry);
        }
        else
        {
            dictionary.Add(category, [entry]);
        }
    }
}
