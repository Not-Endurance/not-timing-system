using Not.Application.RPC.SignalR;
using Not.Events;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Judge.Blazor.Features.Socket;

namespace NTS.Judge.Tests.Blazor;

public class JudgeStartupEventCoordinatorTests
{
    [Fact]
    public async Task RunAtStartupAsync_WhenAlreadyConnected_DoesNothing()
    {
        var socketService = new TestSocketService { IsConnected = true, Event = CreateEvent(7) };
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new JudgeStartupEnduranceEventCoordinator(
            new TestEnduranceEventService([CreateEvent(1), CreateEvent(2)]),
            socketService,
            dialogLauncher
        );

        await coordinator.RunAtStartupAsync();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, dialogLauncher.ShowCalls);
    }

    [Fact]
    public async Task RunAtStartupAsync_WhenNoActiveEvents_DoesNothing()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new JudgeStartupEnduranceEventCoordinator(
            new TestEnduranceEventService([]),
            socketService,
            dialogLauncher
        );

        await coordinator.RunAtStartupAsync();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(0, dialogLauncher.ShowCalls);
    }

    [Fact]
    public async Task RunAtStartupAsync_WhenSingleActiveEvent_ConnectsToIt()
    {
        var activeEvent = CreateEvent(11);
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new JudgeStartupEnduranceEventCoordinator(
            new TestEnduranceEventService([activeEvent]),
            socketService,
            dialogLauncher
        );

        await coordinator.RunAtStartupAsync();

        Assert.Equal(1, socketService.ConnectCalls);
        Assert.Equal(activeEvent.Id, socketService.Event?.Id);
        Assert.Equal(0, dialogLauncher.ShowCalls);
    }

    [Fact]
    public async Task RunAtStartupAsync_WhenMultipleActiveEvents_ShowsSelectionDialog()
    {
        var socketService = new TestSocketService();
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new JudgeStartupEnduranceEventCoordinator(
            new TestEnduranceEventService([CreateEvent(1), CreateEvent(2)]),
            socketService,
            dialogLauncher
        );

        await coordinator.RunAtStartupAsync();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(1, dialogLauncher.ShowCalls);
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            new PopulatedPlace(country, "Sofia", "Sofia"),
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

        public Task<IEnumerable<EnduranceEvent>> GetEvents()
        {
            return Task.FromResult(_events);
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        public int ConnectCalls { get; private set; }
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

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestDialogLauncher : IJudgeSelectEventDialogLauncher
    {
        public int ShowCalls { get; private set; }

        public Task ShowAsync()
        {
            ShowCalls++;
            return Task.CompletedTask;
        }
    }
}
