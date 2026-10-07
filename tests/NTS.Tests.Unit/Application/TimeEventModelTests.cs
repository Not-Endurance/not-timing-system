using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// A time event goes into the document of its Participation and comes out of it as the kind it was (ADR-0005): the two kinds
/// a Snapshot makes and the two an Update makes, each with the marker where it has one, its outcome, the method, the instant
/// it was recorded and the user that recorded it, and the id it was given, which is the id of the Snapshot it records.
/// </summary>
public sealed class TimeEventModelTests
{
    static readonly Timestamp TIME = new(new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.FromHours(2)));
    static readonly DateTimeOffset RECORDED = new(2026, 1, 1, 7, 31, 0, TimeSpan.Zero);
    static readonly Guid ACTOR = TestId.Of(9);

    [Fact]
    public void An_arrive_update_comes_out_of_its_document_as_an_arrive_update()
    {
        var updated = new ArriveUpdated(
            TIME,
            TimeEventOutcome.Accepted,
            SnapshotMethod.Manual,
            RECORDED,
            ACTOR,
            TestId.Of(1)
        );

        var read = TimeEventModel.MapFrom(updated).MapToEntity();

        var arriveUpdated = Assert.IsType<ArriveUpdated>(read);
        Assert.Equal(TestId.Of(1), arriveUpdated.Id);
        Assert.Equal(TIME, arriveUpdated.Time);
        Assert.Equal(TimeEventOutcome.Accepted, arriveUpdated.Outcome);
        Assert.Equal(SnapshotMethod.Manual, arriveUpdated.Method);
        Assert.Equal(RECORDED, arriveUpdated.RecordedAt);
        Assert.Equal(ACTOR, arriveUpdated.ActorId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_present_update_comes_out_of_its_document_as_a_present_update_with_the_marker_it_had(bool isRepresent)
    {
        var updated = new PresentUpdated(
            TIME,
            isRepresent,
            TimeEventOutcome.RejectedInvalidTime,
            SnapshotMethod.Manual,
            RECORDED,
            ACTOR,
            TestId.Of(2)
        );

        var read = TimeEventModel.MapFrom(updated).MapToEntity();

        var presentUpdated = Assert.IsType<PresentUpdated>(read);
        Assert.Equal(isRepresent, presentUpdated.IsRepresent);
        Assert.Equal(TestId.Of(2), presentUpdated.Id);
        Assert.Equal(TimeEventOutcome.RejectedInvalidTime, presentUpdated.Outcome);
        Assert.Equal(TIME, presentUpdated.Time);
    }

    [Fact]
    public void The_kinds_a_snapshot_makes_still_come_out_as_they_were()
    {
        var arrived = new Arrived(TIME, TimeEventOutcome.Accepted, SnapshotMethod.RFID, RECORDED, ACTOR, TestId.Of(3));
        var presented = new Presented(
            TIME,
            true,
            TimeEventOutcome.Accepted,
            SnapshotMethod.Manual,
            RECORDED,
            ACTOR,
            TestId.Of(4)
        );

        Assert.IsType<Arrived>(TimeEventModel.MapFrom(arrived).MapToEntity());
        Assert.True(Assert.IsType<Presented>(TimeEventModel.MapFrom(presented).MapToEntity()).IsRepresent);
    }

    [Fact]
    public void Each_kind_is_stored_under_a_kind_of_its_own()
    {
        TimeEvent[] events =
        [
            new Arrived(TIME, TimeEventOutcome.Accepted, SnapshotMethod.Manual, RECORDED, ACTOR),
            new Presented(TIME, false, TimeEventOutcome.Accepted, SnapshotMethod.Manual, RECORDED, ACTOR),
            new ArriveUpdated(TIME, TimeEventOutcome.Accepted, SnapshotMethod.Manual, RECORDED, ACTOR),
            new PresentUpdated(TIME, false, TimeEventOutcome.Accepted, SnapshotMethod.Manual, RECORDED, ACTOR),
        ];

        var kinds = events.Select(x => TimeEventModel.MapFrom(x).Kind).ToArray();

        Assert.Equal(
            [TimeEventKind.Arrived, TimeEventKind.Presented, TimeEventKind.ArriveUpdated, TimeEventKind.PresentUpdated],
            kinds
        );
    }
}
