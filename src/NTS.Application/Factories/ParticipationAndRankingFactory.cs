using Not.Domain.Exceptions;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Application.Factories;

// TODO: Move Judge/Features/Core/State when consolidating Judge & Witness projects
public static class ParticipationAndRankingFactory
{
    public static (
        List<Participation> Participations,
        Dictionary<ParticipationCategory, List<RankingEntry>> RankingEntriesByCategory
    ) Create(
        Domain.Setup.Aggregates.UpcomingEvents.Competition setupCompetition,
        IEnumerable<Participation> existingParticipations,
        int eventId
    )
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
            var participation = CreateParticipation(setupCompetition, setupParticipation, eventId);

            if (existingParticipations.All(p => p.Combination.Number != participation.Combination.Number))
            {
                participations.Add(participation);
                var rankingEntry = new RankingEntry(participation, null, setupParticipation.IsNotRanked);
                AddRanking(rankingEntriesByCategory, setupParticipation.Category, rankingEntry);
            }
            else
            {
                var participationRef = existingParticipations
                    .ToList()
                    .Find(p => p.Combination.Number == participation.Combination.Number);
                if (participationRef != null)
                {
                    var rankingEntry = new RankingEntry(participationRef, null, setupParticipation.IsNotRanked);
                    AddRanking(rankingEntriesByCategory, setupParticipation.Category, rankingEntry);
                }
            }
        }
        return (participations, rankingEntriesByCategory);
    }

    public static Participation CreateParticipation(
        Domain.Setup.Aggregates.UpcomingEvents.Competition setupCompetition,
        Domain.Setup.Aggregates.UpcomingEvents.Participation setupParticipation,
        int eventId
    )
    {
        var phases = CreatePhases(setupCompetition);
        var totalDistance = setupCompetition.Phases.Sum(x => (decimal)x.Loop!.Distance);
        var combination = CreateCombination(
            setupParticipation.Combination,
            totalDistance,
            setupParticipation.MinAverageSpeed,
            setupParticipation.MaxAverageSpeed
        );
        return new Participation(
            setupParticipation.Category,
            new(setupCompetition.Name, setupCompetition.Ruleset, setupCompetition.Type),
            combination,
            new(phases),
            null,
            eventId
        );
    }

    static Combination CreateCombination(
        Domain.Setup.Aggregates.UpcomingEvents.Combination combination,
        decimal totalDistance,
        double? minAverageSpeed,
        double? maxAverageSpeed
    )
    {
        var setupAthlete = combination.Athlete;
        var setupHorse = combination.Horse;
        var setupClub = combination.Athlete.Club;

        var club = setupClub == null ? null : new Club(setupClub.Name, setupClub.Id);
        var athlete = new Athlete(setupAthlete.Names, setupAthlete.Country, club, setupAthlete.FeiId, setupAthlete.Id);
        var horse = new Horse(setupHorse.Name, setupHorse.FeiId, setupHorse.Id);
        return new Combination(
            combination.Number,
            athlete,
            horse,
            athlete.Club,
            Combination.FormatDistance(totalDistance),
            minAverageSpeed,
            maxAverageSpeed,
            combination.Id
        );
    }

    static List<Phase> CreatePhases(Domain.Setup.Aggregates.UpcomingEvents.Competition setupCompetition)
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
                "",
                setupPhase.Loop!.Distance,
                setupPhase.Recovery,
                setupPhase.Rest,
                setupCompetition.Ruleset,
                isFinal,
                setupCompetition.CompulsoryThresholdSpan,
                Timestamp.Create(startTime),
                null,
                null,
                null,
                false,
                false,
                false
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
