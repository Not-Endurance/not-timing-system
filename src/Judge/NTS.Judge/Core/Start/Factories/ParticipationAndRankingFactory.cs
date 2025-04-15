using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Judge.Core.Start.Factories;

public static class ParticipationAndRankingFactory
{
    public static (
        List<Participation> Participations,
        Dictionary<AthleteCategory, List<RankingEntry>> RankingEntriesByCategory
    ) Create(Domain.Setup.Aggregates.Competition setupCompetition, IEnumerable<Participation> existingParticipations)
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
        foreach (var setupParticipation in setupCompetition.Participations)
        {
            DateTimeOffset? startTime = setupCompetition.Start.ToUniversalTime();
            var setupPhases = setupCompetition.Phases;
            var phases = new List<Phase>();
            foreach (var setupPhase in setupPhases)
            {
                var isFinal = setupPhases.Last() == setupPhase;
                if (!isFinal && setupPhase.Rest == null)
                {
                    throw new DomainException(Invalid_phase_configuration_in_competition__missing_rest, setupCompetition.Name);
                }
                
                var corePhase = new Phase(
                    setupPhase.Loop!.Distance,
                    setupPhase.Recovery,
                    setupPhase.Rest,
                    setupCompetition.Ruleset,
                    isFinal,
                    setupCompetition.CompulsoryThresholdSpan,
                    startTime
                );
                startTime = null; //Set only first phase StartTime
                phases.Add(corePhase);
                competitionDistance += (decimal)setupPhase.Loop!.Distance;
            }
            var setupCombination = setupParticipation.Combination;
            var combination = new Combination(
                setupCombination.Number,
                setupCombination.Athlete,
                setupCombination.Horse,
                competitionDistance,
                setupParticipation.MinAverageSpeed,
                setupParticipation.MaxAverageSpeed
            );
            var participation = new Participation(
                setupCompetition.Name,
                setupCompetition.Ruleset,
                setupCompetition.Type,
                combination,
                phases
            );
            if (existingParticipations.All(p => p.Combination.Number != participation.Combination.Number))
            {
                participations.Add(participation);
                var rankingEntry = new RankingEntry(participation, setupParticipation.IsNotRanked);
                AddRanking(rankingEntriesByCategory, setupCombination.Athlete.Category, rankingEntry);
            }
            else
            {
                var participationRef = existingParticipations
                    .ToList()
                    .Find(p => p.Combination.Number == participation.Combination.Number);
                if (participationRef != null)
                {
                    var rankingEntry = new RankingEntry(participationRef, setupParticipation.IsNotRanked);
                    AddRanking(rankingEntriesByCategory, setupCombination.Athlete.Category, rankingEntry);
                }
            }
        }
        return (participations, rankingEntriesByCategory);
    }

    static void AddRanking(
        Dictionary<AthleteCategory, List<RankingEntry>> dictionary,
        AthleteCategory category,
        RankingEntry entry
    )
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
