using NoTiming.Ui.Features.Core.Participations;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The store through its public surface (#622, ADR-0006): the Participations of the Event in one place, kept current
/// by re-reading the Participation an event names, never by what the event carries.
/// </summary>
public sealed class ParticipationStoreTests
{
    [Fact]
    public async Task A_change_notification_replaces_the_Participation_it_names_with_the_one_the_repository_holds()
    {
        var named = ParticipationFixtures.Active(7);
        var other = ParticipationFixtures.Active(8);
        var repository = new ControlledParticipationRepository(named, other);
        var store = new ParticipationStore(repository, new FakeSocketContext());
        await store.Load();
        var holdsNow = ParticipationFixtures.Completed(7);
        repository.Store(holdsNow);

        await store.Handle(new ParticipationChanged(FakeSocketContext.EVENT_ID, TestId.Of(7)), CancellationToken.None);

        Assert.Same(holdsNow, store.Find(TestId.Of(7)));
        Assert.Same(other, store.Find(TestId.Of(8)));
        Assert.Equal([TestId.Of(7)], repository.Reads); // one read by id, of the Participation it names
    }

    [Fact]
    public async Task A_change_notification_for_another_Event_is_ignored()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(7));
        var store = new ParticipationStore(repository, new FakeSocketContext());
        await store.Load();

        await store.Handle(new ParticipationChanged(TestId.Of(999), TestId.Of(7)), CancellationToken.None); // not this Event

        Assert.Empty(repository.Reads);
    }

    [Fact]
    public async Task An_event_about_a_Participation_the_store_has_not_seen_adds_it()
    {
        var known = ParticipationFixtures.Active(1);
        var repository = new ControlledParticipationRepository(known);
        var store = new ParticipationStore(repository);
        await store.Load();
        var added = ParticipationFixtures.Active(2);
        repository.Store(added);

        await Publish(store, added);

        Assert.Equal([known, added], store.Participations);
    }

    [Fact]
    public async Task Events_that_overlap_for_one_Participation_cause_one_read_in_flight_and_exactly_one_trailing_read()
    {
        var id = TestId.Of(1);
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        await store.Load();
        repository.Hold = true;

        var first = Publish(store, ParticipationFixtures.Active(1));
        var second = Publish(store, ParticipationFixtures.Active(1));
        var third = Publish(store, ParticipationFixtures.Active(1));

        Assert.Equal(1, repository.InFlight(id)); // one read, however many events came during it
        Assert.Single(repository.Reads);
        Assert.False(first.IsCompleted);

        var holdsAfterTheFirstRead = ParticipationFixtures.Completed(1);
        repository.Store(holdsAfterTheFirstRead);
        await repository.Release(id);
        await Until(() => repository.InFlight(id) == 1 && repository.Reads.Count == 2); // the trailing read
        Assert.Same(holdsAfterTheFirstRead, store.Find(id)); // the first read is applied as it finishes
        Assert.False(third.IsCompleted); // and the events wait for the trailing read

        var holdsAfterTheTrailingRead = ParticipationFixtures.Eliminated(1);
        repository.Store(holdsAfterTheTrailingRead);
        await repository.Release(id);
        await Task.WhenAll(first, second, third);

        Assert.Equal(2, repository.Reads.Count); // exactly one more, not one per event
        Assert.Same(holdsAfterTheTrailingRead, store.Find(id));
    }

    [Fact]
    public async Task Events_for_different_Participations_do_not_wait_for_each_other()
    {
        var one = TestId.Of(1);
        var two = TestId.Of(2);
        var repository = new ControlledParticipationRepository(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        var store = new ParticipationStore(repository);
        await store.Load();
        repository.Hold = true;

        var first = Publish(store, ParticipationFixtures.Active(1));
        var second = Publish(store, ParticipationFixtures.Active(2));

        Assert.Equal(1, repository.InFlight(one));
        Assert.Equal(1, repository.InFlight(two)); // started at once, while the first is still being read
        await repository.Release(two);
        await second;
        Assert.False(first.IsCompleted);
        await repository.Release(one);
        await first;
    }

    [Fact]
    public async Task Connecting_loads_the_Participations_and_disconnecting_clears_them_and_tells_the_views()
    {
        var repository = new ControlledParticipationRepository(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Completed(2)
        );
        var store = new ParticipationStore(repository);
        var changes = 0;
        store.ObservableEvent.Subscribe(() => changes++);

        await store.Handle(new EventConnected(TestId.Of(100)), CancellationToken.None);

        Assert.Equal(2, store.Participations.Count);
        Assert.Equal(1, changes);

        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);

        Assert.Empty(store.Participations);
        Assert.Equal(2, changes);

        await store.Handle(new EventConnected(TestId.Of(100)), CancellationToken.None);

        Assert.Equal(2, store.Participations.Count); // the next Event is read from the start
        Assert.Equal(2, repository.ReadManyCalls);
    }

    [Fact]
    public async Task A_read_that_finishes_after_the_Event_was_left_does_not_bring_its_Participation_back()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        await store.Load();
        repository.Hold = true;
        var refreshing = Publish(store, ParticipationFixtures.Active(1));

        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);
        await repository.Release(TestId.Of(1));
        await refreshing;

        Assert.Empty(store.Participations);
    }

    [Fact]
    public async Task A_load_that_finishes_after_the_Event_was_left_does_not_fill_the_store()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        repository.Hold = true;
        var connecting = store.Handle(new EventConnected(TestId.Of(100)), CancellationToken.None);
        await Until(() => repository.ReadManyCalls == 1);

        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);
        await repository.ReleaseMany();
        await connecting;

        Assert.Empty(store.Participations);
    }

    [Fact]
    public async Task An_event_that_arrives_after_the_Event_was_left_is_not_read_and_adds_nothing()
    {
        var socket = new FakeSocketContext();
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository, socket);
        await store.Load();
        socket.Leave();
        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);
        var late = ParticipationFixtures.Active(2);
        repository.Store(late);

        await Publish(store, late); // a message that was already on its way

        Assert.Empty(store.Participations);
        Assert.Empty(repository.Reads);
    }

    [Fact]
    public async Task Events_that_are_applied_on_different_threads_are_all_kept()
    {
        const int count = 64;
        for (var round = 0; round < 25; round++)
        {
            var repository = new ControlledParticipationRepository(
                [.. Enumerable.Range(1, count).Select(ParticipationFixtures.Active)]
            );
            var store = new ParticipationStore(repository);
            await store.Load();
            var newer = Enumerable.Range(1, count).Select(ParticipationFixtures.Completed).ToArray();
            foreach (var participation in newer)
            {
                repository.Store(participation);
            }

            await Task.WhenAll(newer.Select(x => Task.Run(() => Publish(store, x))));

            Assert.All(newer, x => Assert.Same(x, store.Find(x.Id))); // none of the updates was overwritten
        }
    }

    [Fact]
    public async Task An_event_that_arrives_while_the_first_load_is_reading_keeps_its_newer_Participation()
    {
        var id = TestId.Of(1);
        var older = ParticipationFixtures.Active(1);
        var repository = new ControlledParticipationRepository(older);
        var store = new ParticipationStore(repository);
        repository.Hold = true;
        var loading = store.Load();
        await Until(() => repository.ReadManyCalls == 1); // the load has asked, and will answer with the older one
        var newer = ParticipationFixtures.Completed(1);
        repository.Store(newer);
        var refreshing = Publish(store, older);

        await repository.Release(id);
        await refreshing;
        Assert.Same(newer, store.Find(id));
        await repository.ReleaseMany(); // the load finishes last, with what it read first
        await loading;

        Assert.Same(newer, store.Find(id));
    }

    [Fact]
    public async Task An_eliminated_or_completed_Participation_stays_in_the_store_for_the_views_to_show_or_hide()
    {
        var repository = new ControlledParticipationRepository(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        var store = new ParticipationStore(repository);
        await store.Load();
        var eliminated = ParticipationFixtures.Eliminated(1);
        var completed = ParticipationFixtures.Completed(2);
        repository.Store(eliminated);
        repository.Store(completed);

        await Publish(store, eliminated);
        await Publish(store, completed);

        Assert.Equal([eliminated, completed], store.Participations);
    }

    [Fact]
    public async Task A_Participation_the_repository_no_longer_has_leaves_the_store()
    {
        var gone = ParticipationFixtures.Active(1);
        var kept = ParticipationFixtures.Active(2);
        var repository = new ControlledParticipationRepository(gone, kept);
        var store = new ParticipationStore(repository);
        await store.Load();
        repository.Remove(gone.Id);

        await Publish(store, gone);

        Assert.Equal([kept], store.Participations);
    }

    [Fact]
    public async Task A_read_that_fails_reaches_the_event_and_the_next_event_reads_again()
    {
        var id = TestId.Of(1);
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        await store.Load();
        repository.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Publish(store, ParticipationFixtures.Active(1)));
        repository.Fail = false;
        var holdsNow = ParticipationFixtures.Completed(1);
        repository.Store(holdsNow);
        await Publish(store, ParticipationFixtures.Active(1));

        Assert.Same(holdsNow, store.Find(id)); // the failed read did not leave the Participation stuck
        Assert.Equal(2, repository.Reads.Count);
    }

    [Fact]
    public async Task Every_change_tells_the_views_that_observe_the_store()
    {
        var repository = new ControlledParticipationRepository(ParticipationFixtures.Active(1));
        var store = new ParticipationStore(repository);
        await store.Load();
        var changes = 0;
        store.ObservableEvent.Subscribe(() => changes++);
        repository.Store(ParticipationFixtures.Completed(1));

        await Publish(store, ParticipationFixtures.Active(1));

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Loading_reads_every_Participation_of_the_Event_eliminated_and_completed_ones_included()
    {
        var active = ParticipationFixtures.Active(1);
        var completed = ParticipationFixtures.Completed(2);
        var eliminated = ParticipationFixtures.Eliminated(3);
        var store = new ParticipationStore(new ControlledParticipationRepository(active, completed, eliminated));

        await store.Load();

        Assert.Equal([active, completed, eliminated], store.Participations);
        Assert.Same(completed, store.Find(completed.Id));
        Assert.Null(store.Find(TestId.Of(99)));
    }

    /// <summary>Waits, without a fixed delay, until what the store does on its own has happened.</summary>
    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not come true.");
            await Task.Delay(1);
        }
    }

    /// <summary>Delivers the change notification of this Participation, as the hub client does.</summary>
    static Task Publish(ParticipationStore store, Participation changed)
    {
        return store.Handle(ParticipationFixtures.Changed(changed), CancellationToken.None);
    }
}
