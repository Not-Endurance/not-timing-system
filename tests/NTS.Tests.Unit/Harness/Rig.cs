using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Features.Core.EventViews;
using NoTiming.Ui.Features.Core.Participations;
using NoTiming.Ui.Features.Sessions;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Snapshots;
using NTS.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using static NTS.Tests.Unit.Harness.ViewedEventFixtures;

namespace NTS.Tests.Unit.Harness;

internal sealed class Rig
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
        Kept = new DurableStore(Timer);
    }

    public SnapshotService Service { get; private set; }
    public NoSession Session { get; } = new();
    public Publisher Publisher { get; } = new();
    public DurableStore Kept { get; }
    public StepTimer Timer { get; } = new();
    public FakeAccount Account { get; } = new(TestId.Of(1));
    public RecordingNotifier Notifier { get; } = new();

    /// <summary>The page is loaded again: the service of the old one is gone, and the new one stands on what the device kept.</summary>
    public async Task<SnapshotService> ReloadAsync()
    {
        Service?.Dispose();
        Service = new SnapshotService(_socket, _store, Session, Publisher, Account, Kept.Repository, Notifier);
        await Service.Load();
        return Service;
    }

    public async Task<IViewedEvent> ViewAsync(bool canSendSnapshots = true)
    {
        return await LiveView(_store, canSendSnapshots);
    }

    /// <summary>The Event the app follows, opened, with what the person may do about it in the hands of the test.</summary>
    public async Task<(IViewedEvent View, AccessOf Access)> ViewWithAccessAsync()
    {
        var access = new AccessOf(canSendSnapshots: true);
        return (await OpenAsync(LIVE_EVENT_ID, access), access);
    }

    /// <summary>Another Live Event, opened, as a person who may write to it sees it.</summary>
    public Task<IViewedEvent> ViewOfAnotherEventAsync()
    {
        return OpenAsync(OTHER_LIVE_EVENT_ID, new AccessOf(canSendSnapshots: true));
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

    /// <summary>The Event the app follows, opened, with what is told of the end of the Event kept for the test.</summary>
    public Task<IViewedEvent> ViewThatTellsAsync(List<Guid> ended)
    {
        return OpenAsync(LIVE_EVENT_ID, new AccessOf(canSendSnapshots: true), ended.Add);
    }

    async Task<IViewedEvent> OpenAsync(Guid eventId, AccessOf access, Action<Guid>? ended = null)
    {
        var view = new LiveEventView(
            EventOf(eventId),
            _store,
            access,
            new ReadOnlyRepository<Ranking>([]),
            new ReadOnlyRepository<Official>([]),
            ended
        );
        await view.Load();
        return view;
    }
}
