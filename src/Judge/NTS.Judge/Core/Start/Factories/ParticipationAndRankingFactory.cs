using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Enums;

namespace NTS.Judge.Core.Start.Factories;

public static class ParticipationAndRankingFactory
{
    public static (
        List<Participation> Participations,
        Dictionary<ParticipationCategory, List<RankingEntry>> RankingEntriesByCategory
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

        var participations = new List<Participation>();
        var rankingEntriesByCategory = new Dictionary<ParticipationCategory, List<RankingEntry>>();
        foreach (var setupParticipation in setupCompetition.Participations)
        {
            var participation = CreateParticipation(setupCompetition, setupParticipation);
            
            if (existingParticipations.All(p => p.Combination.Number != participation.Combination.Number))
            {
                participations.Add(participation);
                var rankingEntry = new RankingEntry(participation, setupParticipation.IsNotRanked);
                AddRanking(rankingEntriesByCategory, setupParticipation.Category, rankingEntry);
            }
            else
            {
                var participationRef = existingParticipations
                    .ToList()
                    .Find(p => p.Combination.Number == participation.Combination.Number);
                if (participationRef != null)
                {
                    var rankingEntry = new RankingEntry(participationRef, setupParticipation.IsNotRanked);
                    AddRanking(rankingEntriesByCategory, setupParticipation.Category, rankingEntry);
                }
            }
        }
        return (participations, rankingEntriesByCategory);
    }

    static Participation CreateParticipation(
        Domain.Setup.Aggregates.Competition setupCompetition,
        Domain.Setup.Aggregates.Participation setupParticipation)
    {
        var phases = CreatePhases(setupCompetition);
        var totalDistance = setupCompetition.Phases.Sum(x => (decimal)x.Loop!.Distance);
        var combination = new Combination(
            setupParticipation.Combination.Number,
            setupParticipation.Combination.Athlete,
            setupParticipation.Combination.Horse,
            totalDistance,
            setupParticipation.MinAverageSpeed,
            setupParticipation.MaxAverageSpeed
        );
        return new Participation(
            setupCompetition.Name,
            setupParticipation.Category,
            setupCompetition.Ruleset,
            setupCompetition.Type,
            combination,
            phases
        );
    }

    static List<Phase> CreatePhases(Domain.Setup.Aggregates.Competition setupCompetition)
    {
        DateTimeOffset? startTime = setupCompetition.Start.ToUniversalTime();
        var setupPhases = setupCompetition.Phases;
        var phases = new List<Phase>();
        foreach (var setupPhase in setupPhases)
        {
            var isFinal = setupPhases.Last() == setupPhase;
            if (!isFinal && setupPhase.Rest == null)
            {
                throw new DomainException(
                    Invalid_phase_configuration_in_competition__missing_rest,
                    setupCompetition.Name
                );
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
        }
        return phases;
    }

    static void AddRanking(
        Dictionary<ParticipationCategory, List<RankingEntry>> dictionary,
        ParticipationCategory category,
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
