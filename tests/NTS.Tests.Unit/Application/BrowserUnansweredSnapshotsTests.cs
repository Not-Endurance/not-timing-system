using Microsoft.JSInterop;
using NoTiming.Ui.Storage.Browser;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The group that was sent and not answered is kept in the local storage of the browser (#645, ADR-0013): by person and
/// Event, as it was made, so that it is sent again as the same group. It is read back as it was kept, by nobody else and for
/// no other Event; what is there and is not a group is no group; and a storage that cannot be used keeps nothing and says
/// nothing, as the group is then sent from the page.
/// </summary>
public sealed class BrowserUnansweredSnapshotsTests
{
    static readonly Guid ANA = TestId.Of(1);
    static readonly Guid BORIS = TestId.Of(2);
    static readonly Guid EVENT = TestId.Of(10);
    static readonly Guid OTHER_EVENT = TestId.Of(11);

    [Fact]
    public async Task A_group_comes_back_as_it_was_made_with_its_id_its_kind_its_Snapshots_and_the_instants()
    {
        var kept = new BrowserUnansweredSnapshots(new FakeLocalStorage());
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

        await kept.Keep(ANA, EVENT, group);
        var read = await kept.Read(ANA, EVENT);

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
        var kept = new BrowserUnansweredSnapshots(new FakeLocalStorage());
        await kept.Keep(ANA, EVENT, Group(7));

        Assert.NotNull(await kept.Read(ANA, EVENT));
        Assert.Null(await kept.Read(BORIS, EVENT)); // somebody else who uses the device
        Assert.Null(await kept.Read(ANA, OTHER_EVENT)); // another Event
    }

    [Fact]
    public async Task What_is_kept_again_takes_the_place_of_what_was_kept_and_what_is_forgotten_is_not_read()
    {
        var browser = new FakeLocalStorage();
        var kept = new BrowserUnansweredSnapshots(browser);
        var second = Group(8);
        await kept.Keep(ANA, EVENT, Group(7));
        await kept.Keep(ANA, EVENT, second);

        Assert.Equal(second.Id, (await kept.Read(ANA, EVENT))!.Id);
        Assert.Single(browser.Items);

        await kept.Forget(ANA, EVENT);

        Assert.Null(await kept.Read(ANA, EVENT));
        Assert.Empty(browser.Items);
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
    public async Task What_is_there_and_is_not_a_group_that_can_be_sent_is_no_group_and_is_taken_away(string text)
    {
        var browser = new FakeLocalStorage();
        var kept = new BrowserUnansweredSnapshots(browser);
        browser.Put($"nts.unanswered-snapshots.{ANA}.{EVENT}", text);

        Assert.Null(await kept.Read(ANA, EVENT));
        Assert.Empty(browser.Items);
    }

    [Fact]
    public async Task What_is_there_and_is_not_even_text_that_can_be_read_is_no_group()
    {
        var browser = new FakeLocalStorage();
        browser.Put($"nts.unanswered-snapshots.{ANA}.{EVENT}", "{ not json");

        Assert.Null(await new BrowserUnansweredSnapshots(browser).Read(ANA, EVENT));
    }

    [Fact]
    public async Task A_storage_that_cannot_be_used_keeps_nothing_and_says_nothing()
    {
        var kept = new BrowserUnansweredSnapshots(
            new FakeLocalStorage { Fails = new JSException("Access is denied.") }
        );

        await kept.Keep(ANA, EVENT, Group(7));
        await kept.Forget(ANA, EVENT);

        Assert.Null(await kept.Read(ANA, EVENT));
    }

    static SnapshotGroup Group(int number)
    {
        return new SnapshotGroup(
            [new Snapshot(number, "Rider " + number, null, new Timestamp(DateTimeOffset.UtcNow))],
            SnapshotType.Arrive
        );
    }
}
