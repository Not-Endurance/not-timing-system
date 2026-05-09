using Not.Application.RPC;
using Not.Events;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Witness.Blazor.Features.Socket;

namespace NTS.Authentication.Tests;

public class WitnessEventConnectionCoordinatorTests
{
    [Fact]
    public async Task EnsureConnected_WhenAlreadyConnected_DoesNothing()
    {
        var socketService = new TestSocketService { IsConnected = true, Event = CreateEvent(7) };
        var eventInformationService = new TestEventInformationService([CreateEvent(1)]);
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new EventConnectionCoordinator(eventInformationService, socketService, dialogLauncher);

        await coordinator.EnsureConnected();

        Assert.Equal(0, eventInformationService.GetActiveCalls);
        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenNoActiveEvents_DoesNothing()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new EventConnectionCoordinator(
            new TestEventInformationService([]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenSingleActiveEvent_ConnectsToIt()
    {
        var activeEvent = CreateEvent(11);
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new EventConnectionCoordinator(
            new TestEventInformationService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, socketService.ConnectCalls);
        Assert.Equal(activeEvent.Id, socketService.Event?.Id);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenSingleActiveEventWouldResetSession_ConnectsWithoutPrompt()
    {
        var activeEvent = CreateEvent(17);
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new EventConnectionCoordinator(
            new TestEventInformationService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, socketService.ConnectCalls);
        Assert.Equal(activeEvent.Id, socketService.Event?.Id);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenMultipleActiveEvents_ShowsSelectionDialog()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new EventConnectionCoordinator(
            new TestEventInformationService([CreateEvent(1), CreateEvent(2)]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, dialogLauncher.ShowSelectEventCalls);
        Assert.Equal(0, socketService.ConnectCalls);
    }

    static EventInformation CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
            country,
            $"Event{id}",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class TestEventInformationService : IEventInformationService
    {
        readonly IEnumerable<EventInformation> _events;

        public TestEventInformationService(IEnumerable<EventInformation> events)
        {
            _events = events;
        }

        public int GetActiveCalls { get; private set; }

        public Task<IEnumerable<EventInformation>> GetActive()
        {
            GetActiveCalls++;
            return Task.FromResult(_events);
        }

        public Task<IEnumerable<EventInformation>> GetPast()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        public int ConnectCalls { get; private set; }
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public SocketConnectionStatus Status { get; set; } = SocketConnectionStatus.Disconnected;
        public EventInformation? Event { get; set; }

        public Task Connect(EventInformation eventInformation)
        {
            ConnectCalls++;
            IsConnected = true;
            Status = SocketConnectionStatus.Connected;
            Event = eventInformation;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            IsConnected = false;
            Status = SocketConnectionStatus.Disconnected;
            Event = null;
            return Task.CompletedTask;
        }

        public Task<bool> WillResetSession(EventInformation eventInformation)
        {
            return Task.FromResult(false);
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestDialogLauncher : IEventConnectionDialogLauncher
    {
        public int ShowSelectEventCalls { get; private set; }

        public Task ShowSelectEventAsync()
        {
            ShowSelectEventCalls++;
            return Task.CompletedTask;
        }
    }
}
