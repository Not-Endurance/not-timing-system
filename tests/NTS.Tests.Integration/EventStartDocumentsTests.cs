using NoTiming.Api.Features.Reference;
using NTS.Application.Factories;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using SetupAthlete = NTS.Domain.Setup.Aggregates.Athlete;
using SetupClub = NTS.Domain.Setup.Aggregates.Club;
using SetupCombination = NTS.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NTS.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NTS.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NTS.Domain.Setup.Aggregates.Horse;
using SetupLoop = NTS.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupParticipation = NTS.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NTS.Domain.Setup.Aggregates.ConfigureEvents.Phase;

namespace NTS.Tests.Integration;

/// <summary>
/// What starting an Event makes from its Setup (#607, ADR-0006): one document for the Event and the documents it copies and
/// creates. The code that says so is shared by the Api that starts an Event and the command that seeds one, and these tests
/// are of what it makes, not of either of them: a combination that rides in two competitions is one Participation, which
/// the Ranking of each competition counts.
/// </summary>
public sealed class EventStartDocumentsTests
{
    [Fact]
    public void A_combination_that_rides_in_two_competitions_is_one_Participation_that_both_Rankings_count()
    {
        var setup = SetupOfTwoCompetitions();

        var documents = EventStartDocuments.From(setup, new RegionalRules(true));

        Assert.Equal(3, documents.Participations.Count);
        Assert.Equal(2, documents.Rankings.Count);
        var participations = documents.Participations.Select(x => x["_id"].AsGuid).Order().ToArray();
        foreach (var ranking in documents.Rankings)
        {
            Assert.Equal(
                participations,
                ranking["Entries"].AsBsonArray.Select(x => x["ParticipationId"].AsGuid).Order().ToArray()
            );
        }
    }

    [Fact]
    public void The_Officials_and_Operators_the_Setup_names_are_copied_with_the_id_of_the_Event()
    {
        var setup = SetupOfTwoCompetitions();

        var documents = EventStartDocuments.From(setup, new RegionalRules(true));

        Assert.Empty(documents.Officials);
        Assert.Empty(documents.Operators);
        Assert.All(documents.Participations, x => Assert.Equal(setup.Id, x["EventId"].AsGuid));
        Assert.All(documents.Rankings, x => Assert.Equal(setup.Id, x["EventId"].AsGuid));
    }

    static SetupConfigureEvent SetupOfTwoCompetitions()
    {
        // The models are written as the Api writes them: the first date or id a process serializes fixes how all are.
        ApiMongo.Configure();
        var country = new Country(Guid.NewGuid(), "Bulgaria", "BG", "BUL", "bg-BG");
        var loop = new SetupLoop(20, Guid.NewGuid());
        var club = new SetupClub("A club", Guid.NewGuid());
        var combinations = Enumerable
            .Range(1, 3)
            .Select(number => new SetupCombination(
                number,
                new SetupAthlete(
                    $"Rider {number}",
                    $"Rider {number}",
                    $"1001{number:0000}",
                    country,
                    club,
                    Guid.NewGuid()
                ),
                new SetupHorse($"Horse {number}", $"Horse {number}", $"103AA{number:00}", Guid.NewGuid()),
                Guid.NewGuid()
            ))
            .ToList();
        SetupCompetition Competition(string name)
        {
            return new SetupCompetition(
                name,
                CompetitionRuleset.FEI,
                new DateTimeOffset(2031, 3, 14, 6, 0, 0, TimeSpan.Zero),
                compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
                minSpeedRestriction: 12.0,
                maxSpeedRestriction: 25.0,
                feiEventId: "EVENT",
                feiEventCode: "CEI1",
                feiCompetitionId: "COMPETITION",
                feiRule: "Rule",
                feiScheduleNumber: "1",
                [new SetupPhase(loop, recovery: 15, rest: null, id: Guid.NewGuid())],
                combinations
                    .Select(x => new SetupParticipation(
                        isNotRanked: false,
                        combination: x,
                        category: ParticipationCategory.Senior,
                        startTimeOverride: null,
                        maxSpeedOverride: null,
                        minSpeedOverride: null,
                        id: Guid.NewGuid()
                    ))
                    .ToList(),
                id: Guid.NewGuid()
            );
        }

        return new SetupConfigureEvent(
            "Two competitions",
            "Sofia",
            country,
            "TWO42",
            [Competition("50 km"), Competition("80 km")],
            [],
            [loop],
            combinations,
            Guid.NewGuid(),
            [],
            "country-bg",
            Guid.NewGuid()
        );
    }
}
