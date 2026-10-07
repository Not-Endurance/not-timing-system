using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// The id the device makes for each Snapshot of a group it sends (#644, ADR-0013). The server records a Snapshot once whatever
/// the number of times it is sent, by its id, so the id of a Snapshot is the same every time the same group is sent, whatever
/// became of the first time, and another group, which is another gesture, is other Snapshots: sending the Snapshots that were
/// sent as Arrivals again as Presentations has to record them as Presentations.
/// </summary>
public sealed class SnapshotGroupTests
{
    static readonly DateTimeOffset NOW = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_id_of_a_Snapshot_of_a_group_is_the_same_every_time_it_is_asked()
    {
        var group = GroupOf(1, 2, 3);

        var first = group.Entries.Select(group.IdOf).ToArray();
        var again = group.Entries.Select(group.IdOf).ToArray();

        Assert.Equal(first, again);
    }

    [Fact]
    public void The_Snapshots_of_a_group_have_ids_of_their_own_none_of_which_is_empty()
    {
        var group = GroupOf(1, 2, 3, 40, 500);

        var ids = group.Entries.Select(group.IdOf).ToArray();

        Assert.Equal(5, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Fact]
    public void A_group_made_again_from_the_same_Snapshots_makes_other_ids()
    {
        var first = GroupOf(1, 2);
        var again = new SnapshotGroup(first.Entries, SnapshotType.Present);

        var firstIds = first.Entries.Select(first.IdOf).ToArray();
        var againIds = again.Entries.Select(again.IdOf).ToArray();

        Assert.Empty(firstIds.Intersect(againIds));
    }

    [Fact]
    public void A_group_made_with_the_id_of_one_that_was_sent_has_that_id_and_makes_the_ids_it_made()
    {
        var first = GroupOf(1, 2);
        var again = new SnapshotGroup(first.Entries, first.Type, first.Id);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Entries.Select(first.IdOf), again.Entries.Select(again.IdOf));
    }

    [Fact]
    public void Two_groups_of_the_same_numbers_do_not_share_an_id()
    {
        var one = GroupOf(1, 2, 3);
        var other = GroupOf(1, 2, 3);

        Assert.Empty(one.Entries.Select(one.IdOf).Intersect(other.Entries.Select(other.IdOf)));
    }

    static SnapshotGroup GroupOf(params int[] numbers)
    {
        return new SnapshotGroup(
            numbers.Select(x => new Snapshot(x, $"Athlete {x}", null, new Timestamp(NOW.AddMinutes(x)))),
            SnapshotType.Arrive
        );
    }
}
