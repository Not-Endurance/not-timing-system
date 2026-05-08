using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Events;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features.Socket;

namespace NTS.Judge.Tests.Blazor;

public class JudgeStartupEventCoordinatorTests
{
    [Fact]
    public async Task RunAtStartupAsync_WhenAlreadyConnected_DoesNothing()
    {
        var socketService = new TestSocketService { IsConnected = true, Event = CreateEvent(7) };
        var dialogLauncher = new TestDialogLauncher();
        var coordinator = new JudgeStartupEventInformationCoordinator(
            new TestEventInformationService([CreateEvent(1), CreateEvent(2)]),
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
        var coordinator = new JudgeStartupEventInformationCoordinator(
            new TestEventInformationService([]),
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
        var coordinator = new JudgeStartupEventInformationCoordinator(
            new TestEventInformationService([activeEvent]),
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
        var coordinator = new JudgeStartupEventInformationCoordinator(
            new TestEventInformationService([CreateEvent(1), CreateEvent(2)]),
            socketService,
            dialogLauncher
        );

        await coordinator.RunAtStartupAsync();

        Assert.Equal(0, socketService.ConnectCalls);
        Assert.Equal(1, dialogLauncher.ShowCalls);
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

        public IEventSubscriber ObservableEvent => throw new NotImplementedException();

        public Task<IEnumerable<EventInformation>> GetActive()
        {
            return Task.FromResult(_events);
        }

        public Task<IEnumerable<EventInformation>> GetPast()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task Load()
        {
            return Task.CompletedTask;
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
