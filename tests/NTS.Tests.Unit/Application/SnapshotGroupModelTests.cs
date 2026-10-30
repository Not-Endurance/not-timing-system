using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// What a person keeps of a group they sent (#618): the id it was sent under and the instant of each of its Snapshots, which a
/// correction of one of them names; and what was kept before, which has neither.
/// </summary>
public sealed class SnapshotGroupModelTests
{
    [Fact]
    public void A_group_that_is_kept_is_given_back_with_its_id_and_the_instant_of_each_time_with_its_date()
    {
        var yesterday = new DateTimeOffset(2026, 10, 11, 23, 59, 30, TimeSpan.FromHours(3));
        var group = new SnapshotGroup(
            [new Snapshot(7, "Rider 7", "Rider 7 in English", new Timestamp(yesterday))],
            SnapshotType.Present
        );

        var back = SnapshotGroupModel.MapFrom(group).MapToDomain();

        Assert.Equal(group.Id, back.Id);
        Assert.Equal(SnapshotType.Present, back.Type);
        var entry = Assert.Single(back.Entries);
        Assert.Equal(yesterday, entry.Timestamp!.ToDateTimeOffset()); // the same instant, and not the time of the day of today
        Assert.Equal(group.IdOf(Assert.Single(group.Entries)), back.IdOf(entry));
    }

    [Fact]
    public void A_group_kept_before_the_ids_were_has_none_and_its_times_are_the_times_of_the_day_only()
    {
        var legacy = new SnapshotGroupModel
        {
            Type = SnapshotType.Arrive,
            Entries =
            [
                new SnapshotModel
                {
                    Number = 3,
                    Name = "Rider 3",
                    Timestamp = "10:15:30",
                },
            ],
        };

        var back = legacy.MapToDomain();

        Assert.Null(legacy.GroupId);
        Assert.NotEqual(Guid.Empty, back.Id);
        Assert.Equal("10:15:30", Assert.Single(back.Entries).Timestamp!.ToString());
    }

    [Fact]
    public void A_copy_of_a_group_keeps_its_id_and_the_instants()
    {
        var group = new SnapshotGroup(
            [new Snapshot(7, "Rider 7", null, new Timestamp(DateTimeOffset.Now.AddDays(-1)))],
            SnapshotType.Arrive
        );
        var model = SnapshotGroupModel.MapFrom(group);

        var copy = model.Copy();

        Assert.Equal(model.GroupId, copy.GroupId);
        Assert.Equal(model.Entries[0].Time, copy.Entries[0].Time);
        Assert.NotNull(copy.Entries[0].Time);
        Assert.NotSame(model.Entries[0], copy.Entries[0]);
    }
}
