using NoTiming.Ui.Storage.Browser;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Tests.Unit.Harness;

namespace NTS.Tests.Unit.NoTiming.Ui.Storage.Browser;

/// <summary>
/// The group that was sent and not answered is kept in the local storage of the browser (#645, ADR-0013): under its own id, by
/// person and Event, as it was made, so that it is sent again as the same group. It is read back by the person and for the Event
/// it was kept for, and by nobody else; what is there and is not a group is no group; and a storage that cannot be used keeps
/// nothing and says nothing, as the group is then sent from the page.
/// </summary>
public sealed class BrowserDurableSnapshotGroupTests
{
    static readonly Guid ANA = TestId.Of(1);
    static readonly Guid BORIS = TestId.Of(2);
    static readonly Guid EVENT = TestId.Of(10);
    static readonly Guid OTHER_EVENT = TestId.Of(11);

    [Fact]
    public async Task A_group_comes_back_as_it_was_made_with_its_id_its_kind_its_Snapshots_and_the_instants()
    {
        var store = new DurableStore(new StepTimer());
        var group = new SnapshotGroup(
            [
                new Snapshot(
                    7,
                    "Ana Petrova",
                    "Ana Petrova EN",
                    new Timestamp(new DateTimeOffset(2030, 5, 17, 9, 30, 15, 123, TimeSpan.FromHours(3))),
                    CompetitionRuleset.Regional
                ),
                new Snapshot(8, "Boris", null, new Timestamp(new DateTimeOffset(2030, 5, 17, 9, 31, 0, TimeSpan.Zero))),
            ],
            SnapshotType.Present
        );

        await store.Repository.KeepDurably(group.Id, DurableSnapshotGroup.Of(group, ANA, EVENT));
        var read = await GroupOf(store, ANA, EVENT);

        Assert.NotNull(read);
        Assert.Equal(group.Id, read.Id);
        Assert.Equal(SnapshotType.Present, read.Type);
        Assert.Equal(SnapshotIds.Of(group), SnapshotIds.Of(read));
        Assert.Equal(SnapshotIds.Times(group), SnapshotIds.Times(read));
        Assert.Equal(
            [("Ana Petrova", "Ana Petrova EN", CompetitionRuleset.Regional), ("Boris", null, null)],
            read.Entries.Select(x => (x.Name, x.NameEnglish, x.Ruleset))
        );
    }

    [Fact]
    public async Task A_group_is_read_by_the_person_and_for_the_Event_it_was_kept_for_and_by_nobody_else()
    {
        var store = new DurableStore(new StepTimer());
        var group = Group(7);
        await store.Repository.KeepDurably(group.Id, DurableSnapshotGroup.Of(group, ANA, EVENT));

        Assert.Single(await Pending(store, ANA, EVENT));
        Assert.Empty(await Pending(store, BORIS, EVENT)); // somebody else who uses the device
        Assert.Empty(await Pending(store, ANA, OTHER_EVENT)); // another Event
    }

    [Fact]
    public async Task A_group_kept_again_with_its_id_takes_its_place_and_another_group_waits_beside_it_and_a_forgotten_one_is_not_read()
    {
        var store = new DurableStore(new StepTimer());
        var first = Group(7);
        var second = Group(8);
        await store.Repository.KeepDurably(first.Id, DurableSnapshotGroup.Of(first, ANA, EVENT));
        await store.Repository.KeepDurably(first.Id, DurableSnapshotGroup.Of(first, ANA, EVENT));
        await store.Repository.KeepDurably(second.Id, DurableSnapshotGroup.Of(second, ANA, EVENT));

        Assert.Equal(2, store.Count);

        await store.Repository.ForgetDurably(first.Id);

        Assert.Equal(second.Id, Assert.Single(await Pending(store, ANA, EVENT)).Id);
        Assert.Equal(1, store.Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData(
        """{"id":"00000000-0000-0000-0000-000000000000","kind":"Arrive","entries":[{"number":1,"name":"Ana","time":"2030-05-17T09:30:00+00:00"}]}"""
    )]
    [InlineData("""{"id":"3f2504e0-4f89-41d3-9a0c-0305e82c3301","kind":"Arrive","entries":[]}""")]
    [InlineData(
        """{"id":"3f2504e0-4f89-41d3-9a0c-0305e82c3301","kind":"Arrive","entries":[{"number":1,"name":" ","time":"2030-05-17T09:30:00+00:00"}]}"""
    )]
    public async Task What_is_there_and_is_not_a_group_that_can_be_sent_is_no_group(string text)
    {
        // The store keeps what is an item and is no group, and the group it reads is none: the store does not take it away.
        var store = new DurableStore(new StepTimer());
        store.Browser.Put(DurableSnapshotGroup.KEY_PREFIX + TestId.Of(3).ToString("D"), text);

        var pending = await store.Repository.ReadPendingDurably(_ => true);

        Assert.Null(Assert.Single(pending).ToGroup());
    }

    [Fact]
    public async Task What_is_there_and_is_not_even_text_that_can_be_read_is_no_group_and_is_taken_away()
    {
        var store = new DurableStore(new StepTimer());
        store.Browser.Put(DurableSnapshotGroup.KEY_PREFIX + TestId.Of(3).ToString("D"), "{ not json");

        Assert.Empty(await store.Repository.ReadPendingDurably(_ => true));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task A_storage_that_cannot_be_used_keeps_nothing_and_does_not_hold_the_group_back()
    {
        // The page sends a group through StoreDurably: a backup the browser refuses does not hold the request back, so the
        // remote is asked all the same, and nothing is kept.
        var store = new DurableStore(new StepTimer()) { CannotKeep = true };
        var group = Group(7);
        var asked = 0;

        var final = await store.Repository.StoreDurably(
            group.Id,
            DurableSnapshotGroup.Of(group, ANA, EVENT),
            item =>
            {
                asked++;
                return Task.FromResult(true);
            }
        );

        Assert.True(final);
        Assert.Equal(1, asked);
        Assert.Equal(0, store.Count);
    }

    static async Task<IReadOnlyList<DurableSnapshotGroup>> Pending(DurableStore store, Guid accountId, Guid eventId)
    {
        return await store.Repository.ReadPendingDurably(x => x.AccountId == accountId && x.EventId == eventId);
    }

    static async Task<SnapshotGroup?> GroupOf(DurableStore store, Guid accountId, Guid eventId)
    {
        return (await Pending(store, accountId, eventId)).Select(x => x.ToGroup()).OfType<SnapshotGroup>().FirstOrDefault();
    }

    static SnapshotGroup Group(int number)
    {
        return new SnapshotGroup(
            [new Snapshot(number, "Rider " + number, null, new Timestamp(DateTimeOffset.UtcNow))],
            SnapshotType.Arrive
        );
    }
}
