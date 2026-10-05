using Not.Domain.Exceptions;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// Where a Snapshot or a request belongs (#612, ADR-0004): one comparison. When the Phase after the current one has a Start
/// and the time is at or after it, the time belongs to that Phase and it becomes current; otherwise it belongs to the
/// current Phase. The time of a Snapshot is its own, and the time of a request is the clock of the server. Every case here
/// is the same ride: Phase 1 starts at 08:00, its Arrival is at 09:00, its Presentation at 09:10 and its Rest of 40 minutes
/// ends at 09:50, which is the Start of Phase 2.
/// </summary>
public sealed class ParticipationPlacementTests
{
    static readonly DateTimeOffset START = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ARRIVE = START.AddHours(1);
    static readonly DateTimeOffset PRESENT = ARRIVE.AddMinutes(10);
    static readonly DateTimeOffset OUT = PRESENT.AddMinutes(40);

    [Fact]
    public void A_snapshot_stamped_before_the_next_Start_is_placed_in_the_current_phase_where_a_second_arrive_is_a_duplicate()
    {
        var participation = AfterPhaseOne();

        var result = participation.Process(Arrive(OUT.AddMinutes(-1)));

        Assert.Equal(SnapshotResultType.NotAppliedDueToDuplicateArrive, result.Type);
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
        Assert.Null(participation.Phases[1].ArriveTime);
    }

    [Fact]
    public void A_snapshot_stamped_at_the_next_Start_is_placed_in_the_next_phase_which_becomes_current()
    {
        var participation = AfterPhaseOne();

        var result = participation.Process(Arrive(OUT));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(OUT, participation.Phases[1].ArriveTime!.ToDateTimeOffset());
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_snapshot_stamped_within_the_old_grace_after_the_Out_time_is_placed_in_the_next_phase()
    {
        var participation = AfterPhaseOne();

        var result = participation.Process(Arrive(OUT.AddMinutes(15)));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_Present_stamped_before_the_next_Start_replaces_the_presentation_of_the_current_phase()
    {
        var participation = AfterPhaseOne();
        var later = PRESENT.AddMinutes(10);

        var result = participation.Process(Present(later));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(later, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
        Assert.Equal(later.AddMinutes(40), participation.Phases[1].StartTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_Present_stamped_after_the_next_Start_is_placed_in_the_next_phase_and_records_a_presentation_without_an_arrival()
    {
        var participation = AfterPhaseOne();
        var presented = OUT.AddMinutes(40);

        var result = participation.Process(Present(presented));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(presented, participation.Phases[1].PresentTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[1].ArriveTime);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(110)]
    public void Without_a_next_Start_a_snapshot_at_any_time_is_placed_in_the_current_phase(int minutesAfterArrival)
    {
        var participation = Ride(CreatePhase(START, ARRIVE, id: TestId.Of(1)), CreatePhase(null, id: TestId.Of(2)));
        var time = ARRIVE.AddMinutes(minutesAfterArrival);

        var result = participation.Process(Present(time));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(time, participation.Phases[0].PresentTime!.ToDateTimeOffset());
        Assert.Null(participation.Phases[1].PresentTime);
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_snapshot_the_next_phase_rejects_does_not_make_it_the_current_phase()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, OUT.AddMinutes(55), id: TestId.Of(2))
        );
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);

        var result = participation.Process(Arrive(OUT.AddMinutes(60)));

        Assert.Equal(SnapshotResultType.NotAppliedDueToDuplicateArrive, result.Type);
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_complete_final_phase_rejects_every_snapshot_as_participation_complete()
    {
        var participation = Ride(CreatePhase(START, ARRIVE, PRESENT, isFinal: true, id: TestId.Of(1)));

        var arrive = participation.Process(Arrive(PRESENT.AddHours(1)));
        var present = participation.Process(Present(PRESENT.AddHours(2)));

        Assert.Equal(SnapshotResultType.NotAppliedDueToParticipationComplete, arrive.Type);
        Assert.Equal(SnapshotResultType.NotAppliedDueToParticipationComplete, present.Type);
        Assert.Equal(PRESENT, participation.Phases[0].PresentTime!.ToDateTimeOffset());
    }

    [Fact]
    public void A_snapshot_that_completes_the_next_phase_starts_the_one_after_it_and_announces_the_phase_that_completed()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2)),
            CreatePhase(null, isFinal: true, id: TestId.Of(3))
        );
        var arrived = OUT.AddMinutes(55);
        var presented = arrived.AddMinutes(10);
        participation.Process(Arrive(arrived));
        participation.DequeueDomainEvents();

        var result = participation.Process(Present(presented));

        Assert.Equal(SnapshotResultType.Applied, result.Type);
        Assert.Equal(presented.AddMinutes(40), participation.Phases[2].StartTime!.ToDateTimeOffset());
        var completed = Assert.Single(participation.DequeueDomainEvents().OfType<PhaseCompleted>());
        Assert.Equal(TestId.Of(2), completed.PhaseId);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_Representation_request_made_before_the_next_Start_applies_to_the_current_phase()
    {
        var participation = AfterPhaseOne();

        participation.ToggleRepresentation(true, OUT.AddMinutes(-20));

        Assert.True(participation.Phases[0].IsReinspectionRequested);
        Assert.False(participation.Phases[1].IsReinspectionRequested);
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void A_Representation_request_made_at_or_after_the_next_Start_applies_to_the_next_phase_and_needs_its_presentation(
        int minutesAfterTheStart
    )
    {
        var participation = AfterPhaseOne();

        var thrown = Assert.Throws<DomainException>(
            () => participation.ToggleRepresentation(true, OUT.AddMinutes(minutesAfterTheStart))
        );

        Assert.Equal(Cannot_require_representation_without_presentation_time, thrown.Message);
        Assert.False(participation.Phases[0].IsReinspectionRequested);
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);
    }

    [Fact]
    public void A_Representation_request_made_in_the_next_phase_applies_there_and_the_phase_becomes_current()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)),
            CreatePhase(OUT, null, OUT.AddMinutes(65), id: TestId.Of(2))
        );
        Assert.Equal(TestId.Of(1), participation.Phases.Current.Id);

        participation.ToggleRepresentation(true, OUT.AddMinutes(70));

        Assert.True(participation.Phases[1].IsReinspectionRequested);
        Assert.False(participation.Phases[0].IsReinspectionRequested);
        Assert.Equal(TestId.Of(2), participation.Phases.Current.Id);
    }

    [Fact]
    public void An_Inspection_request_is_placed_by_the_same_comparison()
    {
        var before = AfterPhaseOne();
        var after = AfterPhaseOne();

        before.ToggleInspection(true, OUT.AddMinutes(-1));
        after.ToggleInspection(true, OUT);

        Assert.True(before.Phases[0].IsRequiredInspectionRequested);
        Assert.False(before.Phases[1].IsRequiredInspectionRequested);
        Assert.False(after.Phases[0].IsRequiredInspectionRequested);
        Assert.True(after.Phases[1].IsRequiredInspectionRequested);
        Assert.Equal(TestId.Of(2), after.Phases.Current.Id);
    }

    [Fact]
    public void Withdrawing_a_request_is_placed_by_the_same_comparison()
    {
        var before = Requested();
        var after = Requested();

        before.ToggleInspection(false, OUT.AddMinutes(-1));
        after.ToggleInspection(false, OUT);

        Assert.False(before.Phases[0].IsRequiredInspectionRequested);
        Assert.True(after.Phases[0].IsRequiredInspectionRequested);
        Assert.Equal(TestId.Of(2), after.Phases.Current.Id);

        static Participation Requested()
        {
            return Ride(
                CreatePhase(START, ARRIVE, PRESENT, isRequiredInspectionRequested: true, id: TestId.Of(1)),
                CreatePhase(OUT, id: TestId.Of(2))
            );
        }
    }

    [Theory]
    [InlineData(-80, 0)]
    [InlineData(-20, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(120, 1)]
    public void The_phase_the_indicators_describe_is_the_phase_a_request_made_then_acts_on(
        int minutesFromTheNextStart,
        int expectedPhase
    )
    {
        var time = OUT.AddMinutes(minutesFromTheNextStart);
        var participation = AfterPhaseOne();
        var acted = AfterPhaseOne();

        acted.ToggleInspection(true, time);

        Assert.Same(participation.Phases[expectedPhase], participation.PhaseAt(time));
        Assert.Equal(
            [expectedPhase == 0, expectedPhase == 1],
            acted.Phases.Select(x => x.IsRequiredInspectionRequested)
        );
    }

    [Fact]
    public void The_indicators_of_the_phase_at_a_time_show_what_was_requested_in_that_phase()
    {
        var participation = Ride(
            CreatePhase(START, ARRIVE, PRESENT, isRequiredInspectionRequested: true, id: TestId.Of(1)),
            CreatePhase(OUT, id: TestId.Of(2))
        );

        var before = participation.PhaseAt(OUT.AddMinutes(-20));
        var after = participation.PhaseAt(OUT.AddMinutes(20));

        Assert.True(before.IsRequiredInspectionRequested);
        Assert.False(after.IsRequiredInspectionRequested);
    }

    /// <summary>Phase 1 is complete and the Start of Phase 2, which has nothing yet, is the Out time of Phase 1.</summary>
    static Participation AfterPhaseOne()
    {
        return Ride(CreatePhase(START, ARRIVE, PRESENT, id: TestId.Of(1)), CreatePhase(OUT, id: TestId.Of(2)));
    }

    static Snapshot Arrive(DateTimeOffset time)
    {
        return new Snapshot(1, SnapshotType.Arrive, SnapshotMethod.Manual, new Timestamp(time));
    }

    static Snapshot Present(DateTimeOffset time)
    {
        return new Snapshot(1, SnapshotType.Present, SnapshotMethod.Manual, new Timestamp(time));
    }

    static Participation Ride(params Phase[] phases)
    {
        const int number = 1;
        var country = new Country(TestId.Of(number), "Bulgaria", "BG", "BUL", "bg-BG");
        var athlete = new Athlete("Athlete", null, country, null, null, TestId.Of(number));
        var horse = new Horse("Horse", null, null, TestId.Of(number));
        var combination = new Combination(number, athlete, horse, null, "20", null, null, TestId.Of(number));

        return new Participation(
            ParticipationCategory.Senior,
            new Competition("Competition", CompetitionRuleset.Regional),
            combination,
            new PhaseCollection(phases),
            null,
            eventId: TestId.Of(1)
        );
    }

    static Phase CreatePhase(
        DateTimeOffset? start,
        DateTimeOffset? arrive = null,
        DateTimeOffset? present = null,
        bool isFinal = false,
        bool isRequiredInspectionRequested = false,
        Guid? id = null
    )
    {
        return new Phase(
            "",
            20,
            40,
            isFinal ? null : 40,
            CompetitionRuleset.Regional,
            isFinal,
            null,
            Timestamp.Create(start),
            Timestamp.Create(arrive),
            Timestamp.Create(present),
            null,
            false,
            isRequiredInspectionRequested,
            false,
            id
        );
    }
}
