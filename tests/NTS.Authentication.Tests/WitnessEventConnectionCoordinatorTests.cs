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
        var enduranceEventService = new TestEnduranceEventService([CreateEvent(1)]);
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new WitnessEventConnectionCoordinator(enduranceEventService, socketService, dialogLauncher);

        await coordinator.EnsureConnected();

        Assert.Equal(0, enduranceEventService.GetActiveEventsCalls);
        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenNoActiveEvents_DoesNothing()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new WitnessEventConnectionCoordinator(
            new TestEnduranceEventService([]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, socketService.WillResetSessionCalls);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenSingleActiveEventAndNoSessionReset_ConnectsToIt()
    {
        var activeEvent = CreateEvent(11);
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new WitnessEventConnectionCoordinator(
            new TestEnduranceEventService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, socketService.WillResetSessionCalls);
        Assert.Equal(1, socketService.ConnectCalls);
        Assert.Equal(activeEvent.Id, socketService.Event?.Id);
        Assert.Equal(0, dialogLauncher.ConfirmSessionResetCalls);
        Assert.Equal(0, dialogLauncher.ShowSelectEventCalls);
    }

    [Fact]
    public async Task EnsureConnected_WhenSingleActiveEventWouldResetSessionAndUserConfirms_ConnectsToIt()
    {
        var activeEvent = CreateEvent(17);
        var socketService = new TestSocketService { WillResetSessionResult = true };
        var dialogLauncher = new TestDialogLauncher { ConfirmSessionResetResult = true };
        var coordinator = new WitnessEventConnectionCoordinator(
            new TestEnduranceEventService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, socketService.WillResetSessionCalls);
        Assert.Equal(1, dialogLauncher.ConfirmSessionResetCalls);
        Assert.Equal(activeEvent.Id, dialogLauncher.ConfirmedEvent?.Id);
        Assert.Equal(1, socketService.ConnectCalls);
        Assert.Equal(activeEvent.Id, socketService.Event?.Id);
    }

    [Fact]
    public async Task EnsureConnected_WhenSingleActiveEventWouldResetSessionAndUserCancels_DoesNotConnect()
    {
        var activeEvent = CreateEvent(19);
        var socketService = new TestSocketService { WillResetSessionResult = true };
        var dialogLauncher = new TestDialogLauncher { ConfirmSessionResetResult = false };
        var coordinator = new WitnessEventConnectionCoordinator(
            new TestEnduranceEventService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, socketService.WillResetSessionCalls);
        Assert.Equal(1, dialogLauncher.ConfirmSessionResetCalls);
        Assert.Equal(0, socketService.ConnectCalls);
        Assert.False(socketService.IsConnected);
    }

    [Fact]
    public async Task EnsureConnected_WhenMultipleActiveEvents_ShowsSelectionDialog()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new WitnessEventConnectionCoordinator(
            new TestEnduranceEventService([CreateEvent(1), CreateEvent(2)]),
            socketService,
            dialogLauncher
        );

        await coordinator.EnsureConnected();

        Assert.Equal(1, dialogLauncher.ShowSelectEventCalls);
        Assert.Equal(0, socketService.WillResetSessionCalls);
        Assert.Equal(0, socketService.ConnectCalls);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
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

    sealed class TestEnduranceEventService : IEnduranceEventService
    {
        readonly IEnumerable<EnduranceEvent> _events;

        public TestEnduranceEventService(IEnumerable<EnduranceEvent> events)
        {
            _events = events;
        }

        public int GetActiveEventsCalls { get; private set; }

        public Task<IEnumerable<EnduranceEvent>> GetActiveEvents()
        {
            GetActiveEventsCalls++;
            return Task.FromResult(_events);
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        public int ConnectCalls { get; private set; }
        public int WillResetSessionCalls { get; private set; }
        public bool WillResetSessionResult { get; init; }
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public SocketConnectionStatus Status { get; set; } = SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }

        public Task Connect(EnduranceEvent enduranceEvent)
        {
            ConnectCalls++;
            IsConnected = true;
            Status = SocketConnectionStatus.Connected;
            Event = enduranceEvent;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            IsConnected = false;
            Status = SocketConnectionStatus.Disconnected;
            Event = null;
            return Task.CompletedTask;
        }

        public Task<bool> WillResetSession(EnduranceEvent enduranceEvent)
        {
            WillResetSessionCalls++;
            return Task.FromResult(WillResetSessionResult);
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestDialogLauncher : IWitnessEventConnectionDialogLauncher
    {
        public int ShowSelectEventCalls { get; private set; }
        public int ConfirmSessionResetCalls { get; private set; }
        public bool ConfirmSessionResetResult { get; init; } = true;
        public EnduranceEvent? ConfirmedEvent { get; private set; }

        public Task ShowSelectEventAsync()
        {
            ShowSelectEventCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> ConfirmSessionResetAsync(EnduranceEvent enduranceEvent)
        {
            ConfirmSessionResetCalls++;
            ConfirmedEvent = enduranceEvent;
            return Task.FromResult(ConfirmSessionResetResult);
        }
    }
}
