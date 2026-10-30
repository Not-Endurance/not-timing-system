using NTS.Contracts.Core.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What the models of the Participation say of its time events (ADR-0005, ADR-0012): an event that was disabled keeps when it was,
/// whichever kind it is, and a Participation that is read by someone who may not see how its times were recorded tells the times
/// each Phase shows and none of the events, nor who recorded them or when, and shows the same times when it is read back.
/// </summary>
public sealed class TimeEventsOfTheModelsTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);
    static readonly Timestamp TIME = new(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.FromHours(2)));

    [Fact]
    public void An_event_keeps_when_it_was_disabled_whichever_kind_it_is()
    {
        var disabled = RECORDED.AddMinutes(7);
        ParticipationEventBase[] events =
        [
            new Arrived(
                TIME,
                TimeEventOutcome.RejectedManually,
                SnapshotMethod.Manual,
                RECORDED,
                ACTOR,
                null,
                disabled
            ),
            new Presented(
                TIME,
                false,
                TimeEventOutcome.RejectedManually,
                SnapshotMethod.Manual,
                RECORDED,
                ACTOR,
                null,
                disabled
            ),
            new ArriveUpdated(
                TIME,
                TimeEventOutcome.RejectedManually,
                SnapshotMethod.Manual,
                RECORDED,
                ACTOR,
                null,
                disabled
            ),
            new PresentUpdated(
                TIME,
                true,
                TimeEventOutcome.RejectedManually,
                SnapshotMethod.Manual,
                RECORDED,
                ACTOR,
                null,
                disabled
            ),
            new PhaseUpdated(
                [new SlotTime(TimeSlot.Arrive, TIME), new SlotTime(TimeSlot.Present, null)],
                TimeEventOutcome.RejectedManually,
                SnapshotMethod.Manual,
                RECORDED,
                ACTOR,
                null,
                disabled
            ),
        ];

        var read = events.Select(x => TimeEventModel.MapFrom(x).MapToEntity()).ToArray();

        Assert.All(read, x => Assert.Equal(disabled, x.DisabledAt));
        Assert.All(read, x => Assert.Equal(TimeEventOutcome.RejectedManually, x.Outcome));
    }

    [Fact]
    public void An_event_that_was_not_disabled_has_no_time_of_disabling()
    {
        var arrived = new Arrived(TIME, TimeEventOutcome.Accepted, SnapshotMethod.Manual, RECORDED, ACTOR);

        var read = TimeEventModel.MapFrom(arrived).MapToEntity();

        Assert.Null(read.DisabledAt);
    }

    [Fact]
    public void The_Participation_read_without_its_time_events_tells_the_times_and_no_events()
    {
        var participation = Ride();
        var model = ParticipationModel.MapFrom(participation);

        var shown = model.ShownTo(false);

        var phase = shown.Phases[0];
        Assert.Empty(phase.Events);
        Assert.Equal(ARRIVE.AddMinutes(3), phase.ArriveTime);
        Assert.Equal(PRESENT, phase.PresentTime);
        Assert.Null(phase.RepresentTime);
        Assert.Equal(START, phase.StartTime);
        Assert.Empty(shown.Phases[1].Events);
        Assert.Null(shown.Phases[1].ArriveTime);
    }

    [Fact]
    public void The_Participation_read_without_its_time_events_shows_the_same_times_when_it_is_read_back()
    {
        var participation = Ride();
        var shown = ParticipationModel.MapFrom(participation).ShownTo(false);

        var read = shown.MapToEntity();

        Assert.Equal(participation.Phases[0].ArriveTime, read.Phases[0].ArriveTime);
        Assert.Equal(participation.Phases[0].PresentTime, read.Phases[0].PresentTime);
        Assert.Equal(participation.Phases[0].RepresentTime, read.Phases[0].RepresentTime);
        Assert.Equal(participation.Phases[0].GetOutTime(), read.Phases[0].GetOutTime());
        Assert.Equal(participation.Phases[0].IsComplete(), read.Phases[0].IsComplete());
        Assert.Equal(participation.Phases[1].StartTime, read.Phases[1].StartTime);
        Assert.Equal(participation.Id, read.Id);
        Assert.Equal(participation.Version, read.Version);
    }

    [Fact]
    public void A_representation_is_told_as_the_time_it_is_and_read_back_as_one()
    {
        var phase = new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            false,
            null,
            Timestamp.Create(START),
            [
                new Arrived(new Timestamp(ARRIVE), TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null),
                new Presented(
                    new Timestamp(PRESENT),
                    false,
                    TimeEventOutcome.Accepted,
                    SnapshotMethod.Manual,
                    null,
                    null
                ),
                new Presented(
                    new Timestamp(PRESENT.AddMinutes(10)),
                    true,
                    TimeEventOutcome.Accepted,
                    SnapshotMethod.Manual,
                    null,
                    null
                ),
            ],
            true,
            false,
            false,
            TestId.Of(1)
        );

        var shown = PhaseModel.MapFrom(phase).ShownTo(false);

        Assert.Equal(PRESENT.AddMinutes(10), shown.RepresentTime);
        Assert.Equal(PRESENT, shown.PresentTime);
        Assert.Equal(PRESENT.AddMinutes(10), shown.MapToEntity().RepresentTime!.ToDateTimeOffset());
        Assert.True(shown.MapToEntity().IsComplete());
        var onlyRepresent = new PhaseModel
        {
            Id = TestId.Of(2),
            Gate = "",
            Length = 20,
            MaxRecovery = 40,
            Ruleset = CompetitionRuleset.Regional,
            RepresentTime = PRESENT.AddMinutes(10),
        };
        Assert.Equal(PRESENT.AddMinutes(10), onlyRepresent.MapToEntity().RepresentTime!.ToDateTimeOffset()); // read from the times, there being no events
    }

    [Fact]
    public void The_Participation_read_by_Staff_keeps_its_events_and_tells_the_times_too()
    {
        var participation = Ride();
        var model = ParticipationModel.MapFrom(participation);

        var shown = model.ShownTo(true);

        var phase = shown.Phases[0];
        Assert.Equal(model.Phases[0].Events.Select(x => x.Id), phase.Events.Select(x => x.Id));
        Assert.Equal(ARRIVE.AddMinutes(3), phase.ArriveTime);
        Assert.Equal(PRESENT, phase.PresentTime);
        Assert.Equal(ACTOR, phase.Events[0].ActorId);
        Assert.Empty(shown.Phases[1].Events);
        Assert.Equal(participation.Phases[0].ArriveTime, shown.MapToEntity().Phases[0].ArriveTime);
    }

    [Fact]
    public void A_time_that_was_disabled_is_not_among_the_times_the_Participation_tells()
    {
        var participation = Ride();
        participation.ChangeTimeEvents([participation.Phases[0].Events[^1].Id], [], RECORDED.AddMinutes(1));

        var phase = ParticipationModel.MapFrom(participation).ShownTo(false).Phases[0];

        Assert.Equal(ARRIVE, phase.ArriveTime);
    }

    [Fact]
    public void Reading_a_Participation_without_its_time_events_leaves_the_one_it_was_made_from_as_it_was()
    {
        var model = ParticipationModel.MapFrom(Ride());

        model.ShownTo(false);

        Assert.NotEmpty(model.Phases[0].Events);
        Assert.Null(model.Phases[0].ArriveTime);
    }

    [Fact]
    public void A_Phase_that_has_events_is_read_from_them_and_not_from_the_times_told_beside_them()
    {
        var model = new PhaseModel
        {
            Id = TestId.Of(1),
            Gate = "",
            Length = 20,
            MaxRecovery = 40,
            Rest = 40,
            Ruleset = CompetitionRuleset.Regional,
            StartTime = START,
            Events =
            [
                TimeEventModel.MapFrom(
                    new Arrived(new Timestamp(ARRIVE), TimeEventOutcome.Accepted, SnapshotMethod.Manual, null, null)
                ),
            ],
            ArriveTime = ARRIVE.AddHours(5),
        };

        var read = model.MapToEntity();

        Assert.Equal(ARRIVE, read.ArriveTime!.ToDateTimeOffset());
    }

    static Participation Ride()
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));
        var first = new Phase(
            "",
            20,
            40,
            40,
            CompetitionRuleset.Regional,
            false,
            null,
            Timestamp.Create(START),
            (Timestamp?)null,
            null,
            null,
            false,
            false,
            false,
            TestId.Of(1)
        );
        var last = new Phase(
            "",
            20,
            40,
            null,
            CompetitionRuleset.Regional,
            true,
            null,
            null,
            (Timestamp?)null,
            null,
            null,
            false,
            false,
            false,
            TestId.Of(2)
        );
        var participation = new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection([first, last]),
            null,
            eventId: TestId.Of(1),
            version: 4
        );
        var arrived = participation.Process(
            new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(ARRIVE)),
            ACTOR,
            RECORDED
        );
        participation.Process(
            new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(PRESENT)),
            ACTOR,
            RECORDED
        );
        participation.UpdateSnapshot(arrived.Id, new Timestamp(ARRIVE.AddMinutes(3)), ACTOR, RECORDED);
        return participation;
    }
}
