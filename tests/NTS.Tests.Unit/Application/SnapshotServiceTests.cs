using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Events;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Localization.NtsStrings;
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
        using var view = await LiveView(store, canSendSnapshots: true);

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
    public async Task A_history_that_cannot_be_kept_does_not_undo_what_the_server_recorded_or_leave_it_to_be_sent_twice()
    {
        var session = new SessionOf(null) { AppendFails = new InvalidOperationException("The host failed (500)") };
        var (service, store, _, publisher) = await ConnectedTo(
            session,
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        service.SelectForSnapshot(service.Participations[0]);
        service.SelectForSnapshot(service.Participations[0]);
        foreach (var snapshot in service.Snapshots.ToArray())
        {
            service.Capture(snapshot);
        }

        using var view = await LiveView(store, canSendSnapshots: true);

        var published = await service.Publish(view, SnapshotType.Arrive);

        Assert.True(published);
        Assert.Single(publisher.Published);
        Assert.Empty(service.Snapshots); // they are not selected, to be sent again
        Assert.Equal([1, 2], Assert.Single(service.History).Entries.Select(x => x.Number)); // and the page shows them sent
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

        using var view = await LiveView(store, canSendSnapshots: true);

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
        using var view = await LiveView(store, canSendSnapshots: true);

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
        using var view = await LiveView(store, canSendSnapshots: true);

        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));

        Assert.Single(service.Snapshots);
        Assert.Empty(service.History);
    }

    [Fact]
    public async Task A_group_whose_answer_was_lost_is_sent_again_as_the_same_group_so_that_what_was_recorded_is_not_recorded_twice()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Publish(view, SnapshotType.Arrive));
        Assert.Equal([1, 2], service.Snapshots.Select(x => x.Number)); // nothing was taken off the page
        publisher.Fail = null;

        var published = await service.Publish(view, SnapshotType.Arrive);

        Assert.True(published);
        var (first, again) = (publisher.Published[0], publisher.Published[1]);
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(first.Entries.Select(first.IdOf), again.Entries.Select(again.IdOf));
        Assert.Equal(first.Id, Assert.Single(service.History).Id); // the history holds the ids the server holds
        Assert.Empty(service.Snapshots);
    }

    [Fact]
    public async Task A_group_of_which_the_server_recorded_none_is_sent_again_as_the_same_group()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 503, null, "The server could not answer.");
        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Answer = (_, id) => SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));

        await service.Publish(view, SnapshotType.Arrive);

        Assert.Equal(publisher.Published[0].Id, publisher.Published[1].Id);
    }

    [Fact]
    public async Task A_group_that_was_not_answered_is_sent_as_a_new_group_when_it_is_sent_as_another_kind()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Fail = null;

        await service.Publish(view, SnapshotType.Present);

        var (first, other) = (publisher.Published[0], publisher.Published[1]);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Empty(first.Entries.Select(first.IdOf).Intersect(other.Entries.Select(other.IdOf)));
    }

    [Fact]
    public async Task A_group_that_was_not_answered_is_sent_as_a_new_group_when_a_time_was_changed_since()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Fail = null;
        service.UpdateTimestamp(service.Snapshots[0], new Timestamp(DateTimeOffset.Now.AddMinutes(-3)));

        await service.Publish(view, SnapshotType.Arrive);

        Assert.NotEqual(publisher.Published[0].Id, publisher.Published[1].Id);
    }

    [Fact]
    public async Task A_group_that_was_not_answered_is_sent_as_a_new_group_when_another_Snapshot_was_added_since()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[0]);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Fail = null;
        service.SelectForSnapshot(service.Participations[0]);
        service.Capture(service.Snapshots[1]);

        await service.Publish(view, SnapshotType.Arrive);

        Assert.NotEqual(publisher.Published[0].Id, publisher.Published[1].Id);
        Assert.Equal([1, 2], publisher.Published[1].Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task A_group_that_was_not_answered_is_sent_as_a_new_group_when_the_same_time_is_of_another_Participation()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        var captured = new Timestamp(DateTimeOffset.Now);
        service.SelectForSnapshot(service.Participations[0]);
        service.UpdateTimestamp(service.Snapshots[0], captured);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Fail = null;
        service.Remove(service.Snapshots[0]);
        service.SelectForSnapshot(service.Participations.Single(x => x.Combination.Number == 2));
        service.UpdateTimestamp(service.Snapshots[0], captured);

        await service.Publish(view, SnapshotType.Arrive);

        Assert.NotEqual(publisher.Published[0].Id, publisher.Published[1].Id);
        Assert.Equal([2], publisher.Published[1].Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task A_group_that_was_recorded_is_sent_as_a_new_group_when_it_is_sent_again_from_the_history()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var sent = Assert.Single(service.History);

        await service.RePublish(view, sent, SnapshotType.Arrive);

        var (first, again) = (publisher.Published[0], publisher.Published[1]);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Empty(first.Entries.Select(first.IdOf).Intersect(again.Entries.Select(again.IdOf)));
    }

    [Fact]
    public async Task A_group_sent_again_from_the_history_is_a_new_group_when_a_time_of_it_was_changed_since()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        var group = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now))],
            SnapshotType.Arrive
        );
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.RePublish(view, group, SnapshotType.Present));
        publisher.Fail = null;
        group.Entries.Single().Timestamp = new Timestamp(DateTimeOffset.Now.AddMinutes(4));

        await service.RePublish(view, group, SnapshotType.Present);

        Assert.NotEqual(publisher.Published[0].Id, publisher.Published[1].Id);
    }

    [Fact]
    public async Task What_a_partly_recorded_group_leaves_is_sent_as_a_new_group()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Answer = (number, id) =>
            number == 2
                ? SnapshotReceipt.Failed(id, 409, "participation-busy", "Too many at once.")
                : SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));
        await Assert.ThrowsAsync<DomainException>(() => service.Publish(view, SnapshotType.Arrive));
        publisher.Answer = (_, id) => SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));

        await service.Publish(view, SnapshotType.Arrive);

        var (first, rest) = (publisher.Published[0], publisher.Published[1]);
        Assert.NotEqual(first.Id, rest.Id);
        Assert.Equal([2], rest.Entries.Select(x => x.Number));
        Assert.Equal([first.Id, rest.Id], service.History.Select(x => x.Id));
    }

    [Fact]
    public async Task Sending_a_group_again_whose_answer_was_lost_keeps_its_ids()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        var group = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now))],
            SnapshotType.Arrive
        );
        using var view = await LiveView(store, canSendSnapshots: true);
        publisher.Fail = new HttpRequestException("The answer was lost.");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.RePublish(view, group, SnapshotType.Present));
        publisher.Fail = null;

        await service.RePublish(view, group, SnapshotType.Present);

        var (first, again) = (publisher.Published[0], publisher.Published[1]);
        Assert.Equal(first.Entries.Select(first.IdOf), again.Entries.Select(again.IdOf));
        Assert.Empty(group.Entries.Select(group.IdOf).Intersect(again.Entries.Select(again.IdOf)));
    }

    [Fact]
    public async Task Sending_a_group_again_sends_other_Snapshots_to_the_Event_the_view_shows_and_tells_the_person_when_one_was_not_recorded()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        var group = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now))],
            SnapshotType.Arrive
        );
        using var view = await LiveView(store, canSendSnapshots: true);

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
        using var view = await LiveView(store, canSendSnapshots: false);

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

    [Fact]
    public async Task The_time_of_the_latest_entry_of_a_start_number_is_corrected_at_the_server_and_the_entry_and_the_kept_history_show_it()
    {
        var session = new SessionOf(null);
        var (service, store, _, publisher) = await ConnectedTo(
            session,
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        var entry = group.Entries.Single(x => x.Number == 1);
        var other = group.Entries.Single(x => x.Number == 2);
        var before = other.Timestamp;
        var corrected = new Timestamp(entry.Timestamp!.ToDateTimeOffset().AddMinutes(-3));

        Assert.True(service.CanUpdate(group, entry));
        await service.UpdateTime(view, group, entry, corrected);

        Assert.Equal([(group.IdOf(entry), corrected.ToDateTimeOffset())], publisher.Updates); // the server names the Snapshot by the id it was sent under
        Assert.Equal(corrected, entry.Timestamp);
        Assert.Equal(before, other.Timestamp); // and no other entry changed
        Assert.Equal([(group.Id, 1, corrected)], session.Updated); // and the history that is kept says so
    }

    [Fact]
    public async Task Only_the_latest_entry_of_a_start_number_and_kind_can_be_corrected_and_an_older_one_is_refused_before_the_server_is_asked()
    {
        var (service, store, _, publisher) = await Connected(
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        using var view = await LiveView(store, canSendSnapshots: true);
        service.SelectForSnapshot(service.Participations[0]);
        service.SelectForSnapshot(service.Participations[0]);
        foreach (var snapshot in service.Snapshots.ToArray())
        {
            service.Capture(snapshot);
        }

        await service.Publish(view, SnapshotType.Arrive); // 1 and 2 as arrivals
        service.SelectForSnapshot(service.Participations.Single(x => x.Combination.Number == 1));
        service.Capture(service.Snapshots.Single());
        await service.Publish(view, SnapshotType.Arrive); // 1 again, as an arrival
        service.SelectForSnapshot(service.Participations.Single(x => x.Combination.Number == 1));
        service.Capture(service.Snapshots.Single());
        await service.Publish(view, SnapshotType.Present); // and 1 as a presentation

        var (first, second, third) = (service.History[0], service.History[1], service.History[2]);
        Assert.False(service.CanUpdate(first, first.Entries.Single(x => x.Number == 1))); // there is a later arrival of 1
        Assert.True(service.CanUpdate(first, first.Entries.Single(x => x.Number == 2))); // none of 2
        Assert.True(service.CanUpdate(second, second.Entries.Single()));
        Assert.True(service.CanUpdate(third, third.Entries.Single())); // another kind of time is another entry

        var refused = await Assert.ThrowsAsync<DomainException>(
            () =>
                service.UpdateTime(
                    view,
                    first,
                    first.Entries.Single(x => x.Number == 1),
                    new Timestamp(DateTimeOffset.Now)
                )
        );

        Assert.Equal(Snapshot_not_the_latest_string, refused.Message);
        Assert.Empty(publisher.Updates);
    }

    [Fact]
    public async Task A_group_that_the_history_kept_with_its_id_is_corrected_by_it_and_one_kept_before_the_ids_were_is_not_corrected_at_all()
    {
        var kept = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now.AddMinutes(-20)))],
            SnapshotType.Arrive
        );
        var legacy = SnapshotGroupModel.MapFrom(
            new SnapshotGroup(
                [new Snapshot(2, "Athlete 2", null, new Timestamp(DateTimeOffset.Now.AddMinutes(-10)))],
                SnapshotType.Arrive
            )
        );
        legacy.GroupId = null;
        var state = new NtsUserSessionStateModel { SnapshotHistory = [SnapshotGroupModel.MapFrom(kept), legacy] };
        var (service, store, _, publisher) = await Connected(
            state,
            ParticipationFixtures.Active(1),
            ParticipationFixtures.Active(2)
        );
        using var view = await LiveView(store, canSendSnapshots: true);
        var (keptGroup, legacyGroup) = (service.History[0], service.History[1]);

        Assert.True(service.CanUpdate(keptGroup, keptGroup.Entries.Single()));
        Assert.False(service.CanUpdate(legacyGroup, legacyGroup.Entries.Single()));
        await service.UpdateTime(view, keptGroup, keptGroup.Entries.Single(), new Timestamp(DateTimeOffset.Now));
        var refused = await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateTime(view, legacyGroup, legacyGroup.Entries.Single(), new Timestamp(DateTimeOffset.Now))
        );

        Assert.Equal(kept.Id, keptGroup.Id);
        Assert.Equal([kept.IdOf(kept.Entries.Single())], publisher.Updates.Select(x => x.Id));
        Assert.Equal(Snapshot_not_the_latest_string, refused.Message);
    }

    [Fact]
    public async Task A_group_of_a_kind_that_an_Update_does_not_name_is_not_corrected_though_it_has_an_id()
    {
        var final = new SnapshotGroup(
            [new Snapshot(1, "Athlete 1", null, new Timestamp(DateTimeOffset.Now.AddMinutes(-5)))],
            SnapshotType.Final
        );
        var state = new NtsUserSessionStateModel { SnapshotHistory = [SnapshotGroupModel.MapFrom(final)] };
        var (service, _, _, _) = await Connected(state, ParticipationFixtures.Active(1));
        var group = Assert.Single(service.History);

        Assert.False(service.CanUpdate(group, group.Entries.Single()));
    }

    [Fact]
    public async Task A_correction_the_server_recorded_and_did_not_take_says_so_and_leaves_the_entry_and_the_kept_history_as_they_were()
    {
        var session = new SessionOf(null);
        var (service, store, _, publisher) = await ConnectedTo(session, ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        var entry = group.Entries.Single();
        var before = entry.Timestamp;
        publisher.UpdateAnswer = id =>
            SnapshotReceipt.Recorded(id, 200, Recorded(TimeEventOutcome.RejectedInvalidTime));

        await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateTime(view, group, entry, new Timestamp(DateTimeOffset.Now.AddHours(-5)))
        );

        Assert.Equal(before, entry.Timestamp);
        Assert.Empty(session.Updated);
        Assert.True(service.CanUpdate(group, entry)); // the person may try another time
    }

    [Fact]
    public async Task A_correction_the_server_refused_is_said_in_its_words_and_a_refusal_of_access_makes_the_app_ask_the_account_again()
    {
        var account = new FakeAccount(TestId.Of(1));
        var session = new SessionOf(null);
        var (service, store, _, publisher) = await ConnectedTo(session, account, ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        var entry = group.Entries.Single();
        publisher.UpdateAnswer = id => SnapshotReceipt.Failed(id, 409, "event-ended", "The Event has ended.");

        var ended = await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateTime(view, group, entry, new Timestamp(DateTimeOffset.Now))
        );
        var refreshesBefore = account.Refreshes;
        publisher.UpdateAnswer = id => SnapshotReceipt.Failed(id, 403, "not-allowed", "You may not send.");
        var forbidden = await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateTime(view, group, entry, new Timestamp(DateTimeOffset.Now))
        );

        Assert.Equal("The Event has ended.", ended.Message);
        Assert.Equal("You may not send.", forbidden.Message);
        Assert.Equal(0, refreshesBefore);
        Assert.Equal(1, account.Refreshes);
        Assert.Empty(session.Updated);
    }

    [Fact]
    public async Task A_correction_is_refused_before_the_server_is_asked_when_the_viewer_may_not_write()
    {
        var (service, store, _, publisher) = await Connected(ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var writer = await LiveView(store, canSendSnapshots: true);
        await service.Publish(writer, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        using var reader = await LiveView(store, canSendSnapshots: false);

        await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateTime(reader, group, group.Entries.Single(), new Timestamp(DateTimeOffset.Now))
        );

        Assert.Empty(publisher.Updates);
    }

    [Fact]
    public async Task A_correction_that_no_answer_came_to_leaves_the_entry_and_is_sent_again_by_the_person()
    {
        var session = new SessionOf(null);
        var (service, store, _, publisher) = await ConnectedTo(session, ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        var entry = group.Entries.Single();
        var before = entry.Timestamp;
        var corrected = new Timestamp(DateTimeOffset.Now.AddMinutes(-2));
        publisher.UpdateFails = new HttpRequestException("The answer was lost.");

        await Assert.ThrowsAsync<HttpRequestException>(() => service.UpdateTime(view, group, entry, corrected));
        Assert.Equal(before, entry.Timestamp);
        publisher.UpdateFails = null;
        await service.UpdateTime(view, group, entry, corrected);

        Assert.Equal(corrected, entry.Timestamp);
        Assert.Equal(2, publisher.Updates.Count);
        Assert.Equal(publisher.Updates[0], publisher.Updates[1]); // the same request, which is absolute
    }

    [Fact]
    public async Task A_history_that_cannot_be_kept_does_not_undo_a_correction_the_server_took()
    {
        var session = new SessionOf(null) { UpdateFails = new InvalidOperationException("The host failed (500)") };
        var (service, store, _, _) = await ConnectedTo(session, ParticipationFixtures.Active(1));
        SelectAndCapture(service);
        using var view = await LiveView(store, canSendSnapshots: true);
        await service.Publish(view, SnapshotType.Arrive);
        var group = Assert.Single(service.History);
        var entry = group.Entries.Single();
        var corrected = new Timestamp(DateTimeOffset.Now.AddMinutes(-2));

        await service.UpdateTime(view, group, entry, corrected);

        Assert.Equal(corrected, entry.Timestamp); // the page shows what the server took
    }

    static void SelectAndCapture(SnapshotService service)
    {
        foreach (var participation in service.Participations.ToArray())
        {
            service.SelectForSnapshot(participation);
        }

        foreach (var snapshot in service.Snapshots.ToArray())
        {
            service.Capture(snapshot);
        }
    }

    static Task<(SnapshotService, ParticipationStore, ControlledParticipationRepository, RecordingPublisher)> Connected(
        params Participation[] participations
    )
    {
        return Connected(null, participations);
    }

    static Task<(
        SnapshotService Service,
        ParticipationStore Store,
        ControlledParticipationRepository Repository,
        RecordingPublisher Publisher
    )> Connected(NtsUserSessionStateModel? session, params Participation[] participations)
    {
        return ConnectedTo(new SessionOf(session), participations);
    }

    static async Task<(
        SnapshotService Service,
        ParticipationStore Store,
        ControlledParticipationRepository Repository,
        RecordingPublisher Publisher
    )> ConnectedTo(SessionOf session, params Participation[] participations)
    {
        return await ConnectedTo(session, new FakeAccount(TestId.Of(1)), participations);
    }

    static async Task<(
        SnapshotService Service,
        ParticipationStore Store,
        ControlledParticipationRepository Repository,
        RecordingPublisher Publisher
    )> ConnectedTo(SessionOf session, FakeAccount account, params Participation[] participations)
    {
        var repository = new ControlledParticipationRepository(participations);
        var socket = new FakeSocketContext();
        var store = new ParticipationStore(repository, socket);
        var publisher = new RecordingPublisher();
        var service = new SnapshotService(
            socket,
            store,
            session,
            publisher,
            account,
            new DurableStore(new StepTimer()).Repository,
            new RecordingNotifier()
        );
        await service.Load();
        return (service, store, repository, publisher);
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

    sealed class SessionOf : IWitnessUserSession
    {
        readonly NtsUserSessionStateModel? _session;

        public SessionOf(NtsUserSessionStateModel? session)
        {
            _session = session;
        }

        /// <summary>What keeping a sent group fails with when a test says so: the host failed or the session ended.</summary>
        public Exception? AppendFails { get; set; }

        /// <summary>What correcting a kept entry fails with when a test says so.</summary>
        public Exception? UpdateFails { get; set; }

        /// <summary>The corrections the service asked to be kept: the id of the group, the start number and the time.</summary>
        public List<(Guid GroupId, int Number, Timestamp Time)> Updated { get; } = [];

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
            return AppendFails == null ? Task.CompletedTask : Task.FromException(AppendFails);
        }

        public Task UpdateSnapshotInHistory(Guid groupId, int number, Timestamp time)
        {
            Updated.Add((groupId, number, time));
            return UpdateFails == null ? Task.CompletedTask : Task.FromException(UpdateFails);
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

    sealed class RecordingPublisher : ISnapshotPublisher
    {
        public List<SnapshotGroup> Published { get; } = [];
        public List<Guid> Events { get; } = [];

        /// <summary>What the request fails with when a test says so: the group is taken by the server or not, and the answer is lost.</summary>
        public Exception? Fail { get; set; }

        /// <summary>What the server says of the Snapshot of a start number with the id it was sent under: recorded and accepted, unless a test says otherwise.</summary>
        public Func<int, Guid, SnapshotReceipt> Answer { get; set; } =
            (_, id) => SnapshotReceipt.Recorded(id, 201, Recorded(TimeEventOutcome.Accepted));

        /// <summary>The Updates the server was asked for: the id of the Snapshot and the instant it was to have.</summary>
        public List<(Guid Id, DateTimeOffset Time)> Updates { get; } = [];

        /// <summary>What the Update fails with when a test says so: the answer is lost.</summary>
        public Exception? UpdateFails { get; set; }

        /// <summary>What the server says of an Update, when a test says something other than what it says of a Snapshot.</summary>
        public Func<Guid, SnapshotReceipt>? UpdateAnswer { get; set; }

        public Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
            Guid eventId,
            SnapshotGroup snapshotGroup,
            CancellationToken cancellationToken = default
        )
        {
            Events.Add(eventId);
            Published.Add(snapshotGroup);
            if (Fail != null)
            {
                throw Fail;
            }

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
            Updates.Add((snapshotId, time));
            if (UpdateFails != null)
            {
                throw UpdateFails;
            }

            return Task.FromResult(UpdateAnswer?.Invoke(snapshotId) ?? Answer(0, snapshotId));
        }
    }
}
