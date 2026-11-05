using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.IntegrationTests.Drivers;
using NoTiming.IntegrationTests.Infrastructure;
using NoTiming.Judge.Contracts.Features.Core;
using SetupAthlete = NoTiming.Domain.Setup.Aggregates.Athlete;
using SetupCombination = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Combination;
using SetupCompetition = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Competition;
using SetupConfigureEvent = NoTiming.Domain.Setup.Aggregates.ConfigureEvent;
using SetupHorse = NoTiming.Domain.Setup.Aggregates.Horse;
using SetupLoop = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Loop;
using SetupParticipation = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Participation;
using SetupPhase = NoTiming.Domain.Setup.Aggregates.ConfigureEvents.Phase;

namespace NoTiming.IntegrationTests.EndToEndEventTests;

[Collection(EndToEndEventCollection.Name)]
public sealed class CompulsoryInspectionSetupTests
{
    readonly NoTimingIntegrationFixture _fixture;

    public CompulsoryInspectionSetupTests(NoTimingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Start_marks_matching_core_phase_as_compulsory_inspection()
    {
        const int eventId = 260521001;
        var setupEvent = CreateSetupEvent(eventId);
        using var api = new NoTimingApiDriver(_fixture);

        await api.CreateSetupConfigureEvent(setupEvent);

        var persistedSetupEvent = await api.ReadSetupConfigureEvent(eventId);
        var persistedPhases = persistedSetupEvent.Competitions.Single().Phases;
        Assert.False(persistedPhases[0].IsCompulsoryInspectionRequired);
        Assert.True(persistedPhases[1].IsCompulsoryInspectionRequired);
        Assert.False(persistedPhases[2].IsCompulsoryInspectionRequired);

        await using var judge = new JudgeDriver(_fixture.NoTimingBaseUrl);
        await judge.Start();
        await judge.GetRequiredService<IDashService>().Start(eventId);

        var participations = await api.ReadParticipations(eventId);
        Assert.Equal(2, participations.Count);
        Assert.All(
            participations,
            participation =>
            {
                Assert.False(participation.Phases[0].IsRequiredInspectionRequested);
                Assert.False(participation.Phases[0].IsRequiredInspectionCompulsory);
                Assert.True(participation.Phases[1].IsRequiredInspectionRequested);
                Assert.True(participation.Phases[1].IsRequiredInspectionCompulsory);
                Assert.False(participation.Phases[2].IsRequiredInspectionRequested);
                Assert.False(participation.Phases[2].IsRequiredInspectionCompulsory);
            }
        );

        var phaseStart = DateTimeOffset.UtcNow.Date.AddDays(30).AddHours(10);
        var explicitCompulsoryPhase = participations[0].Phases[1];
        participations[0]
            .Update(
                new PhaseState(
                    explicitCompulsoryPhase.Id,
                    phaseStart,
                    phaseStart.AddHours(1),
                    phaseStart.AddHours(1).AddMinutes(5),
                    null
                )
            );
        Assert.True(participations[0].Phases[1].IsRequiredInspectionRequested);
        Assert.True(participations[0].Phases[1].IsRequiredInspectionCompulsory);
    }

    static SetupConfigureEvent CreateSetupEvent(int eventId)
    {
        var country = new Country(37, "Bulgaria", "BGR", "BUL", "bg-BG");
        var loop20 = new SetupLoop(20, eventId + 10);
        var loop10 = new SetupLoop(10, eventId + 11);
        var phases = new[]
        {
            new SetupPhase(loop20, recovery: 15, rest: 40, id: eventId + 20),
            new SetupPhase(loop20, recovery: 15, rest: 40, id: eventId + 21, isCompulsoryInspectionRequired: true),
            new SetupPhase(loop10, recovery: 20, rest: null, id: eventId + 22),
        };
        var combinations = new[]
        {
            CreateCombination(eventId + 100, 101, country, "One"),
            CreateCombination(eventId + 200, 102, country, "Two"),
        };
        var participations = combinations
            .Select(
                (combination, index) =>
                    new SetupParticipation(
                        isNotRanked: false,
                        combination: combination,
                        category: ParticipationCategory.Senior,
                        startTimeOverride: null,
                        maxSpeedOverride: null,
                        minSpeedOverride: null,
                        id: eventId + 300 + index
                    )
            )
            .ToArray();
        var competition = new SetupCompetition(
            "Compulsory inspection setup test",
            CompetitionRuleset.Regional,
            DateTimeOffset.UtcNow.Date.AddDays(30).AddHours(8),
            compulsoryThresholdSpan: TimeSpan.FromMinutes(10),
            minSpeedRestriction: null,
            maxSpeedRestriction: null,
            feiEventId: null,
            feiEventCode: null,
            feiCompetitionId: null,
            feiRule: null,
            feiScheduleNumber: null,
            phases,
            participations,
            id: eventId + 1
        );

        return new SetupConfigureEvent(
            "Compulsory inspection setup event",
            "Sofia",
            country,
            feiShowId: null,
            [competition],
            officials: [],
            [loop20, loop10],
            combinations,
            eventId
        );
    }

    static SetupCombination CreateCombination(int idBase, int number, Country country, string suffix)
    {
        var athlete = new SetupAthlete(
            $"Setup Rider{suffix}",
            $"Setup Rider{suffix}",
            feiId: null,
            country,
            club: null,
            id: idBase + 1
        );
        var horse = new SetupHorse($"Setup Horse {suffix}", $"Setup Horse {suffix}", feiId: null, id: idBase + 2);
        return new SetupCombination(number, athlete, horse, idBase + 3);
    }

    sealed class PhaseState : IPhaseState
    {
        public PhaseState(
            int id,
            DateTimeOffset? startTime,
            DateTimeOffset? arriveTime,
            DateTimeOffset? presentTime,
            DateTimeOffset? representTime
        )
        {
            Id = id;
            StartTime = startTime;
            ArriveTime = arriveTime;
            PresentTime = presentTime;
            RepresentTime = representTime;
        }

        public int Id { get; }
        public DateTimeOffset? StartTime { get; }
        public DateTimeOffset? ArriveTime { get; }
        public DateTimeOffset? PresentTime { get; }
        public DateTimeOffset? RepresentTime { get; }
    }
}
