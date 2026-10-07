using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.UserSession;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Tests.Unit.Application.ViewedEventFixtures;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The snapshot page's service is a view over the store (#622): it lists the Participations that are still to be timed
/// and keeps the person's selection, which is page state. It holds no Participation of its own.
/// </summary>
public sealed class SnapshotServiceTests
{
    [Fact]
    public async Task The_list_shows_the_Participations_still_to_be_timed_and_a_selected_one_leaves_it_until_removed()
    {
        var (service, _, _, _) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2),
            ParticipationFixtures.Completed(3),
            ParticipationFixtures.Eliminated(4)
        );
        Assert.Equal([1, 2], service.Participations.Select(x => x.Combination.Number));

        service.SelectForSnapshot(service.Participations[0]);

        Assert.Equal([2], service.Participations.Select(x => x.Combination.Number));
        Assert.Equal([1], service.ParticipationsToSnapshot.Select(x => x.Combination.Number));
        Assert.Equal([1], service.Snapshots.Select(x => x.Number));

        service.Remove(service.Snapshots[0]);

        Assert.Equal([1, 2], service.Participations.Select(x => x.Combination.Number));
        Assert.Empty(service.ParticipationsToSnapshot);
        Assert.Empty(service.Snapshots);
    }

    [Fact]
    public async Task A_selected_Participation_is_the_one_the_store_holds_now_and_stays_off_the_list()
    {
        var (service, store, repository, _) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        service.SelectForSnapshot(service.Participations[0]);
        var arrived = ParticipationFixtures.Arrived(1);
        repository.Store(arrived);

        await store.Handle(ParticipationFixtures.Changed(arrived), CancellationToken.None);

        Assert.Same(arrived, Assert.Single(service.ParticipationsToSnapshot));
        Assert.Equal([2], service.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task An_eliminated_Participation_leaves_the_list_and_a_restored_one_is_back()
    {
        var (service, store, repository, _) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        var eliminated = ParticipationFixtures.Eliminated(1);
        repository.Store(eliminated);

        await store.Handle(ParticipationFixtures.Changed(eliminated), CancellationToken.None);

        Assert.Equal([2], service.Participations.Select(x => x.Combination.Number));

        var restored = ParticipationFixtures.Active(1);
        repository.Store(restored);
        await store.Handle(ParticipationFixtures.Changed(restored), CancellationToken.None);

        Assert.Equal([1, 2], service.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task Publishing_sends_the_timed_snapshots_records_them_and_returns_their_Participations_to_the_list()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        service.SelectForSnapshot(service.Participations[0]);
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await LiveView(store, WitnessAccessLevel.Official);

        var published = await service.Publish(view, SnapshotType.Arrive);

        Assert.True(published);
        var group = Assert.Single(publisher.Published);
        Assert.Equal([LIVE_EVENT_ID], publisher.Events); // to the Event the view shows
        Assert.Equal([1], group.Entries.Select(x => x.Number)); // only the one that was timed
        Assert.Single(service.History);
        Assert.Equal([2], service.Snapshots.Select(x => x.Number)); // the other is still selected
        Assert.Equal([1], service.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task A_Snapshot_the_server_did_not_record_stays_selected_and_the_person_is_told_why_while_the_ones_it_did_leave_the_page()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2),
            ParticipationFixtures.Active(3)
        );
        publisher.Answer = (number, id) =>
            number == 2
                ? SnapshotReceipt.Failed(
                    id,
                    409,
                    "participation-busy",
                    "The Participation was being changed by too many at once."
                )
                : SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));
        foreach (var participation in service.Participations.ToArray())
        {
            service.SelectForSnapshot(participation);
        }

        foreach (var snapshot in service.Snapshots.ToArray())
        {
            service.Capture(snapshot);
        }

        using var view = await LiveView(store, WitnessAccessLevel.Official);

        var refused = await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));

        Assert.Contains("too many at once", refused.Message);
        Assert.Equal([2], service.Snapshots.Select(x => x.Number)); // the one that was not recorded is still to be sent
        var recorded = Assert.Single(service.History);
        Assert.Equal([1, 3], recorded.Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task A_Snapshot_the_server_recorded_as_rejected_has_been_sent_and_leaves_the_page()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        publisher.Answer = (_, id) =>
            SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.RejectedDuplicateArrive));
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await LiveView(store, WitnessAccessLevel.Official);

        var published = await service.Publish(view, SnapshotType.Arrive);

        Assert.True(published);
        Assert.Empty(service.Snapshots);
        Assert.Single(service.History);
    }

    [Fact]
    public async Task When_the_server_records_none_nothing_leaves_the_page_and_nothing_enters_the_history()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 403, "not-allowed", "You may not do this.");
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await LiveView(store, WitnessAccessLevel.Official);

        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));

        Assert.Single(service.Snapshots);
        Assert.Empty(service.History);
    }

    [Fact]
    public async Task Sending_a_group_again_sends_other_Snapshots_to_the_Event_the_view_shows_and_tells_the_person_when_one_was_not_recorded()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        var group = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now))],
            SnapshotType.Arrive
        );
        using var view = await LiveView(store, WitnessAccessLevel.Official);

        await service.RePublish(view, group, SnapshotType.Present);

        var again = Assert.Single(publisher.Published);
        Assert.Equal(SnapshotType.Present, again.Type);
        Assert.Equal([LIVE_EVENT_ID], publisher.Events);
        Assert.Empty(group.Entries.Select(group.IdOf).Intersect(again.Entries.Select(again.IdOf)));

        publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 409, "event-ended", "The Event has ended.");

        await Assert.ThrowsAsync<DomainException>(() => service.RePublish(view, group, SnapshotType.Present));
    }

    [Fact]
    public async Task Publishing_is_refused_before_anything_is_sent_when_the_Event_has_ended()
    {
        var (service, _, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await HistoricView();

        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));

        Assert.Empty(publisher.Published);
        Assert.Single(service.Snapshots); // nothing was sent, so nothing was taken off the page
        Assert.Empty(service.History);
    }

    [Fact]
    public async Task Publishing_is_refused_before_anything_is_sent_to_a_viewer_who_may_not_write()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await LiveView(store, WitnessAccessLevel.Registered);

        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));

        Assert.Empty(publisher.Published);
        Assert.Single(service.Snapshots);
    }

    [Fact]
    public async Task Sending_a_group_again_is_refused_before_anything_is_sent_when_the_Event_has_ended()
    {
        var (service, _, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        var group = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now))],
            SnapshotType.Arrive
        );
        using var view = await HistoricView();

        await Assert.ThrowsAsync<DomainException>(() => service.RePublish(view, group, SnapshotType.Present));

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task A_selection_kept_in_the_session_is_restored_for_the_Participations_still_to_be_timed()
    {
        var session = new NtsUserSessionStateModel
        {
            SnapshotSelections =
            [
                new SnapshotModel { Number = 2, Name = "Athlete 2" },
                new SnapshotModel { Number = 3, Name = "Athlete 3" }, // completed since: not restored
            ],
        };

        var (service, _, _, _) = await Connected(
            session,
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2),
            ParticipationFixtures.Completed(3)
        );

        Assert.Equal([2], service.Snapshots.Select(x => x.Number));
        Assert.Equal([2], service.ParticipationsToSnapshot.Select(x => x.Combination.Number));
        Assert.Equal([1], service.Participations.Select(x => x.Combination.Number));
    }

    [Fact]
    public async Task Leaving_the_Event_clears_the_selection_and_the_history()
    {
        var (service, store, _, _) = await Connected(ParticipationFixtures.Active(1));
        service.SelectForSnapshot(service.Participations[0]);

        await store.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);
        await service.Handle(new EventDisconnected(TestId.Of(100)), CancellationToken.None);

        Assert.Empty(service.Snapshots);
        Assert.Empty(service.ParticipationsToSnapshot);
        Assert.Empty(service.History);
        Assert.Empty(service.Participations);
    }

    static Task<(SnapshotService, ParticipationStore, ControlledParticipationRepository, RecordingPublisher)> Connected(
        params Participation[] participations
    )
    {
        return Connected(null, participations);
    }

    static async Task<(
        SnapshotService Service,
        ParticipationStore Store,
        ControlledParticipationRepository Repository,
        RecordingPublisher Publisher
    )> Connected(NtsUserSessionStateModel? session, params Participation[] participations)
    {
        var repository = new ControlledParticipationRepository(participations);
        var socket = new FakeSocketContext();
        var store = new ParticipationStore(repository, socket);
        var publisher = new RecordingPublisher();
        var service = new SnapshotService(socket, store, new SessionOf(session), publisher);
        await service.Load();
        return (service, store, repository, publisher);
    }

    sealed class SessionOf : IWitnessUserSession
    {
        readonly NtsUserSessionStateModel? _session;

        public SessionOf(NtsUserSessionStateModel? session)
        {
            _session = session;
        }

        public Task<NtsUserSessionStateModel?> GetCurrent()
        {
            return Task.FromResult(_session);
        }

        public Task SetEventId(Guid? eventId)
        {
            return Task.CompletedTask;
        }

        public Task AppendSnapshot(SnapshotGroup snapshot)
        {
            return Task.CompletedTask;
        }

        public Task ReplaceSnapshotSelections(IReadOnlyCollection<Snapshot> snapshots)
        {
            return Task.CompletedTask;
        }

        public Task DeleteCurrent()
        {
            return Task.CompletedTask;
        }
    }

    static RecordedSnapshot Recorded(TimeEventOutcome outcome)
    {
        return new RecordedSnapshot
        {
            EventId = LIVE_EVENT_ID,
            ParticipationId = TestId.Of(1),
            Number = 1,
            Slot = TimeSlot.Arrive,
            Time = DateTimeOffset.Now,
            Outcome = outcome,
        };
    }

    sealed class RecordingPublisher : ISnapshotPublisher
    {
        public List<SnapshotGroup> Published { get; } = [];
        public List<Guid> Events { get; } = [];

        /// <summary>What the server says of the Snapshot of a start number with the id it was sent under: recorded and accepted, unless a test says otherwise.</summary>
        public Func<int, Guid, SnapshotReceipt> Answer { get; set; } =
            (_, id) => SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));

        public Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
            Guid eventId,
            SnapshotGroup snapshotGroup,
            CancellationToken cancellationToken = default
        )
        {
            Events.Add(eventId);
            Published.Add(snapshotGroup);
            IReadOnlyList<SnapshotReceipt> receipts =
            [
                .. snapshotGroup.Entries.Select(x => Answer(x.Number, snapshotGroup.IdOf(x))),
            ];
            return Task.FromResult(receipts);
        }

        public Task<SnapshotReceipt> UpdateSnapshotAsync(
            Guid snapshotId,
            DateTimeOffset time,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(Answer(0, snapshotId));
        }
    }
}
