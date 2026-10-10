using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.Participations;
using NTS.Application.UserSession;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Tests.Unit.Application.ViewedEventFixtures;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// A group of Snapshots that was sent and not answered is the device's until the server answers (#645, ADR-0013): it is
/// kept before it is sent, with its id, so that it is sent again as the same group after the page was closed, reloaded or
/// lost, and again and again until the server answers, and a resend never records a time twice. It is the person's: nobody
/// else who uses the device sends it. A server that refuses it has answered, and it is not sent again.
/// </summary>
public sealed class UnansweredSnapshotsTests
{
    [Fact]
    public async Task A_group_is_kept_before_it_is_sent_and_forgotten_once_the_server_answers()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1), ParticipationFixtures.Active(2));
        rig.Capture(1, 2);
        SnapshotGroup? keptWhenSent = null;
        rig.Publisher.Before = _ => keptWhenSent = rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID);
        using var view = await rig.ViewAsync();

        var published = await rig.Service.Publish(view, SnapshotType.Arrive);

        Assert.True(published);
        Assert.NotNull(keptWhenSent); // it was there when the request went
        Assert.Equal(SnapshotIds.Of(Assert.Single(rig.Publisher.Published)), SnapshotIds.Of(keptWhenSent!));
        Assert.Null(rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID));
        Assert.Null(rig.Service.Unanswered);
        Assert.False(rig.Timer.IsWaiting);
    }

    [Fact]
    public async Task A_group_whose_request_failed_is_kept_with_its_id_and_is_on_its_way_to_be_sent_again()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("No route to the host.");
        using var view = await rig.ViewAsync();

        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));

        var kept = rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID);
        Assert.Equal(SnapshotIds.Of(Assert.Single(rig.Publisher.Published)), SnapshotIds.Of(kept!));
        Assert.NotNull(rig.Service.Unanswered);
        Assert.Equal([1], rig.Service.Snapshots.Select(x => x.Number)); // and it is still selected, to be seen
        Assert.True(rig.Timer.IsWaiting); // and the app is on its way to send it again
    }

    [Fact]
    public async Task After_the_page_was_lost_the_group_is_sent_again_as_the_same_group_and_a_time_is_recorded_once()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1), ParticipationFixtures.Active(2));
        rig.Capture(1, 2);
        rig.Publisher.Fail = new HttpRequestException("The answer was lost.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Present));
        var first = rig.Publisher.Published.Single();
        var service = await rig.ReloadAsync(); // the page was closed, and the app is opened again
        rig.Publisher.Fail = null;
        Assert.Equal(SnapshotIds.Times(first), SnapshotIds.Times(service.Unanswered!)); // with the times it was made with
        Assert.Equal([1, 2], service.Snapshots.Select(x => x.Number)); // and selected again, to be seen

        service.Resume(view);
        rig.Timer.Elapse();
        await rig.Resent(service);

        Assert.Equal(2, rig.Publisher.Published.Count);
        var second = rig.Publisher.Published[1];
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(SnapshotIds.Of(first), SnapshotIds.Of(second)); // the ids are what the server tells a repeat by
        Assert.Equal(SnapshotType.Present, second.Type);
        Assert.Null(rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID));
        Assert.Null(service.Unanswered);
        Assert.Empty(service.Snapshots);
        Assert.Equal([1, 2], Assert.Single(service.History).Entries.Select(x => x.Number));
    }

    [Fact]
    public async Task A_group_is_sent_again_after_longer_and_longer_until_the_server_answers()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));

        for (var attempt = 2; attempt <= 7; attempt++)
        {
            rig.Timer.Elapse();
            await Eventually(() => rig.Timer.Spans.Count == attempt);
        }

        rig.Publisher.Fail = null;
        rig.Timer.Elapse();
        await rig.Resent(rig.Service);

        Assert.Equal([5, 10, 20, 40, 60, 60, 60], rig.Timer.Spans.Select(x => (int)x.TotalSeconds));
        Assert.Equal(8, rig.Publisher.Published.Count);
        Assert.Single(rig.Publisher.Published.Select(x => x.Id).Distinct()); // one group, the whole time
        Assert.Null(rig.Service.Unanswered);
        Assert.Single(rig.Service.History);
    }

    [Fact]
    public async Task A_group_the_server_could_not_take_yet_is_sent_again_and_one_it_refused_is_not()
    {
        var busy = await Rig.StartAsync(ParticipationFixtures.Active(1));
        busy.Capture(1);
        busy.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 409, "participation-busy", "Busy.");
        using var busyView = await busy.ViewAsync();
        await Assert.ThrowsAsync<DomainException>(() => busy.Service.Publish(busyView, SnapshotType.Arrive));

        var refused = await Rig.StartAsync(ParticipationFixtures.Active(1));
        refused.Capture(1);
        refused.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 409, "event-ended", "The Event has ended.");
        using var refusedView = await refused.ViewAsync();
        await Assert.ThrowsAsync<DomainException>(() => refused.Service.Publish(refusedView, SnapshotType.Arrive));

        Assert.NotNull(busy.Service.Unanswered);
        Assert.NotNull(busy.Kept.Of(busy.Account.Current!.Id, LIVE_EVENT_ID));
        Assert.True(busy.Timer.IsWaiting);
        Assert.Null(refused.Service.Unanswered); // answered
        Assert.Null(refused.Kept.Of(refused.Account.Current!.Id, LIVE_EVENT_ID));
        Assert.False(refused.Timer.IsWaiting);
        Assert.Equal([1], refused.Service.Snapshots.Select(x => x.Number)); // it is still the person's, to be seen
    }

    [Theory]
    [InlineData(0, null, true)]
    [InlineData(408, null, true)]
    [InlineData(429, "rate-limited", true)]
    [InlineData(500, null, true)]
    [InlineData(502, null, true)]
    [InlineData(503, null, true)]
    [InlineData(409, "participation-busy", true)]
    [InlineData(400, "invalid-snapshot", false)]
    [InlineData(401, "not-signed-in", false)]
    [InlineData(403, "not-allowed", false)]
    [InlineData(404, "participation-not-found", false)]
    [InlineData(409, "event-ended", false)]
    [InlineData(409, "id-taken", false)]
    [InlineData(422, null, false)]
    public async Task What_the_server_says_decides_whether_a_group_is_sent_again(int status, string? code, bool again)
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, status, code, "The server said so.");
        using var view = await rig.ViewAsync();

        await Assert.ThrowsAsync<DomainException>(() => rig.Service.Publish(view, SnapshotType.Arrive));

        Assert.Equal(again, rig.Timer.IsWaiting);
        Assert.Equal(again, rig.Service.Unanswered != null);
        Assert.Equal(again, rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID) != null);
    }

    [Fact]
    public async Task A_group_that_was_kept_takes_the_place_of_what_the_session_had_of_its_Snapshots()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1), ParticipationFixtures.Active(2));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        var kept = rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID)!;
        var sessionHadFirstAt = new Timestamp(new DateTimeOffset(2030, 5, 21, 6, 0, 0, TimeSpan.Zero));
        var sessionHadSecondAt = new Timestamp(new DateTimeOffset(2030, 5, 21, 7, 0, 0, TimeSpan.Zero));
        rig.Session.Selections =
        [
            new Snapshot(1, "Rider 1", null, sessionHadFirstAt),
            new Snapshot(2, "Rider 2", null, sessionHadSecondAt),
        ];

        var service = await rig.ReloadAsync();

        Assert.Equal([1, 2], service.Snapshots.Select(x => x.Number).Order());
        Assert.Equal(kept.Entries.Single().Timestamp, service.Snapshots.Single(x => x.Number == 1).Timestamp); // the time that was sent
        // and what the session had of the others, which it keeps as the time of day
        Assert.Equal(sessionHadSecondAt.ToString(), service.Snapshots.Single(x => x.Number == 2).Timestamp!.ToString());
    }

    [Fact]
    public async Task A_group_that_is_refused_when_it_is_sent_again_is_said_once_and_not_sent_again()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        rig.Publisher.Fail = null;
        rig.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 409, "event-ended", "The Event has ended.");

        rig.Timer.Elapse();
        await rig.Resent(rig.Service);

        Assert.Equal(["The Event has ended."], rig.Notifier.Warnings);
        Assert.Equal(2, rig.Publisher.Published.Count);
        Assert.Null(rig.Service.Unanswered);
        Assert.Null(rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID));
        Assert.False(rig.Timer.IsWaiting);
    }

    [Fact]
    public async Task A_group_is_the_persons_and_nobody_else_who_uses_the_device_sends_it()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        var calls = rig.Publisher.Published.Count;

        rig.Account.Current = new NTS.Contracts.Features.Account.CurrentAccount
        {
            Id = TestId.Of(2),
            Email = "boris@example.test",
            ProfileComplete = true,
        };
        var other = await rig.ReloadAsync();
        other.Resume(view);

        Assert.Null(other.Unanswered);
        Assert.Empty(other.Snapshots);
        Assert.False(rig.Timer.IsWaiting);
        Assert.Equal(calls, rig.Publisher.Published.Count);
        Assert.NotNull(rig.Kept.Of(TestId.Of(1), LIVE_EVENT_ID)); // and it is still kept for the one it is of
    }

    [Fact]
    public async Task A_group_waits_for_a_person_who_may_send_it_and_is_not_sent_by_one_who_may_not()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        var service = await rig.ReloadAsync();
        using var registered = await rig.ViewAsync(WitnessAccessLevel.Registered);

        service.Resume(registered);

        Assert.False(rig.Timer.IsWaiting);
        Assert.Single(rig.Timer.Spans); // only the wait of the page that was lost
        Assert.NotNull(service.Unanswered);
        Assert.NotNull(rig.Kept.Of(rig.Account.Current!.Id, LIVE_EVENT_ID));
    }

    [Fact]
    public async Task What_is_sent_by_hand_and_what_is_sent_again_are_never_sent_at_the_same_time()
    {
        var rig = await Rig.StartAsync(ParticipationFixtures.Active(1), ParticipationFixtures.Active(2));
        rig.Capture(1);
        rig.Publisher.Fail = new HttpRequestException("Offline.");
        using var view = await rig.ViewAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        rig.Publisher.Fail = null;
        rig.Publisher.Hold();

        rig.Timer.Elapse(); // the app sends it again, and the request is on its way
        await Eventually(() => rig.Publisher.Published.Count == 2);
        var byHand = rig.Service.Publish(view, SnapshotType.Arrive);
        await Task.Delay(50);

        Assert.Equal(2, rig.Publisher.Published.Count); // the one by hand waits for the one on its way
        rig.Publisher.Release();
        Assert.False(await byHand); // and finds that it was answered, and has nothing to send
        await rig.Resent(rig.Service);
        Assert.Equal(2, rig.Publisher.Published.Count);
        Assert.Single(rig.Service.History);
    }

    [Fact]
    public async Task A_refusal_that_says_the_person_may_not_makes_the_account_be_asked_who_is_signed_in_again()
    {
        var notAllowed = await Rig.StartAsync(ParticipationFixtures.Active(1));
        notAllowed.Capture(1);
        notAllowed.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 403, "not-allowed", "You may not.");
        var signedOut = await Rig.StartAsync(ParticipationFixtures.Active(1));
        signedOut.Capture(1);
        signedOut.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 401, "not-signed-in", "Sign in.");
        var ended = await Rig.StartAsync(ParticipationFixtures.Active(1));
        ended.Capture(1);
        ended.Publisher.Answer = (_, id) => SnapshotReceipt.Failed(id, 409, "event-ended", "Ended.");

        foreach (var rig in new[] { notAllowed, signedOut, ended })
        {
            using var view = await rig.ViewAsync();
            await Assert.ThrowsAsync<DomainException>(() => rig.Service.Publish(view, SnapshotType.Arrive));
        }

        Assert.Equal(1, notAllowed.Account.Refreshes); // access that was removed shows on the next request
        Assert.Equal(1, signedOut.Account.Refreshes);
        Assert.Equal(0, ended.Account.Refreshes); // an Event that has ended is not about the person
    }

    static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(condition(), "The app did not do what was waited for.");
    }

    /// <summary>Everything the page stands on: the connection, the store, the host that answers, and what the device keeps.</summary>
    sealed class Rig
    {
        public static async Task<Rig> StartAsync(params Participation[] participations)
        {
            var rig = new Rig(participations);
            await rig.ReloadAsync();
            return rig;
        }

        readonly FakeSocketContext _socket = new();
        readonly ParticipationStore _store;
        readonly ControlledParticipationRepository _repository;

        Rig(Participation[] participations)
        {
            _repository = new ControlledParticipationRepository(participations);
            _store = new ParticipationStore(_repository, _socket);
            Service = null!;
        }

        public SnapshotService Service { get; private set; }
        public NoSession Session { get; } = new();
        public Publisher Publisher { get; } = new();
        public InMemoryUnansweredSnapshots Kept { get; } = new();
        public StepTimer Timer { get; } = new();
        public FakeAccount Account { get; } = new(TestId.Of(1));
        public RecordingNotifier Notifier { get; } = new();

        /// <summary>The page is loaded again: the service of the old one is gone, and the new one stands on what the device kept.</summary>
        public async Task<SnapshotService> ReloadAsync()
        {
            Service?.Dispose();
            Service = new SnapshotService(_socket, _store, Session, Publisher, Account, Kept, Timer, Notifier);
            await Service.Load();
            return Service;
        }

        public async Task<IViewedEvent> ViewAsync(WitnessAccessLevel level = WitnessAccessLevel.Official)
        {
            return await LiveView(_store, level);
        }

        /// <summary>Selects the Participations of the numbers and captures a time for each.</summary>
        public void Capture(params int[] numbers)
        {
            foreach (var number in numbers)
            {
                Service.SelectForSnapshot(Service.Participations.First(x => x.Combination.Number == number));
            }

            foreach (var snapshot in Service.Snapshots.ToArray())
            {
                Service.Capture(snapshot);
            }
        }

        /// <summary>Waits for the app to be done with sending a group that waits again.</summary>
        public async Task Resent(SnapshotService service)
        {
            await service.Resending.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    sealed class NoSession : IWitnessUserSession
    {
        /// <summary>The Snapshots the session kept as selected, if the test says it kept any.</summary>
        public IReadOnlyList<Snapshot>? Selections { get; set; }

        public Task<NtsUserSessionStateModel?> GetCurrent()
        {
            return Task.FromResult(
                Selections == null
                    ? null
                    : new NtsUserSessionStateModel
                    {
                        SnapshotSelections = [.. Selections.Select(SnapshotModel.MapFrom)],
                    }
            );
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

    /// <summary>The host that takes the groups: it answers each Snapshot, or fails the request, or keeps the request until a test lets it go.</summary>
    sealed class Publisher : ISnapshotPublisher
    {
        TaskCompletionSource? _held;

        public List<SnapshotGroup> Published { get; } = [];
        public Action<SnapshotGroup>? Before { get; set; }
        public Exception? Fail { get; set; }

        public Func<int, Guid, SnapshotReceipt> Answer { get; set; } =
            (_, id) => SnapshotReceipt.Recorded(id, 201, new RecordedSnapshot { Outcome = TimeEventOutcome.Accepted });

        public void Hold()
        {
            _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release()
        {
            _held?.SetResult();
        }

        public async Task<IReadOnlyList<SnapshotReceipt>> PublishSnapshotsAsync(
            Guid eventId,
            SnapshotGroup snapshotGroup,
            CancellationToken cancellationToken = default
        )
        {
            Published.Add(snapshotGroup);
            Before?.Invoke(snapshotGroup);
            if (_held != null)
            {
                await _held.Task;
            }

            if (Fail != null)
            {
                throw Fail;
            }

            return [.. snapshotGroup.Entries.Select(x => Answer(x.Number, snapshotGroup.IdOf(x)))];
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
