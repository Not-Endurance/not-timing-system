using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features;
using NTS.Judge.Features.Core;
using NTS.Judge.Features.Socket;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Events;
using Not.Structures;

namespace NTS.Judge.Tests.Blazor;

public class UpcomingEventsListBehindTests
{
    [Fact]
    public void ShouldShowStart_WhenThereAreNoActiveEvents_ReturnsTrue()
    {
        var component = CreateComponent(activeEnduranceEventCount: 0);

        Assert.True(component.ShowStart());
    }

    [Fact]
    public void ShouldShowStart_WhenThereAreActiveEvents_ReturnsFalse()
    {
        var component = CreateComponent(activeEnduranceEventCount: 1);

        Assert.False(component.ShowStart());
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventMatchesAndStarted_ReturnsTrue()
    {
        var component = CreateComponent(isStarted: true, connectedEventId: 7);

        Assert.True(component.ShowReset(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventDiffers_ReturnsFalse()
    {
        var component = CreateComponent(isStarted: true, connectedEventId: 8);

        Assert.False(component.ShowReset(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenNotStarted_ReturnsFalse()
    {
        var component = CreateComponent(isStarted: false, connectedEventId: 7);

        Assert.False(component.ShowReset(CreateUpcomingEvent(7)));
    }

    static TestUpcomingEventsListBehind CreateComponent(
        int activeEnduranceEventCount = 0,
        bool isStarted = false,
        int? connectedEventId = null
    )
    {
        var component = new TestUpcomingEventsListBehind
        {
            ActiveCount = activeEnduranceEventCount,
            Service = new TestDashService { IsStarted = isStarted },
            SocketContext = new TestJudgeSocketService { Event = connectedEventId == null ? null : CreateEnduranceEvent(connectedEventId.Value) },
        };
        return component;
    }

    static UpcomingEvent CreateUpcomingEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new UpcomingEvent("Event", "Sofia", country, null, null, null, [], [], [], [], id);
    }

    static EnduranceEvent CreateEnduranceEvent(int id)
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

    sealed class TestUpcomingEventsListBehind : UpcomingEventsListBehind
    {
        public int ActiveCount
        {
            set => ActiveEnduranceEventCount = value;
        }

        public new IDashService Service
        {
            set => base.Service = value;
        }

        public new JudgeSocketService SocketContext
        {
            set => base.SocketContext = value;
        }

        public bool ShowStart()
        {
            return ShowStartButton();
        }

        public bool ShowReset(UpcomingEvent upcomingEvent)
        {
            return ShowResetTimingButton(upcomingEvent);
        }
    }

    sealed class TestDashService : IDashService
    {
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsStarted { get; set; }

        public Task<Result<IReadOnlyList<NTS.Domain.Setup.Services.StartValidation.StartValidationIssue>>> Start(int upcomingEventId)
        {
            throw new NotImplementedException();
        }

        public Task LoadArchive(int archiveId)
        {
            throw new NotImplementedException();
        }

        public Task Reset()
        {
            throw new NotImplementedException();
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestJudgeSocketService : JudgeSocketService
    {
        public TestJudgeSocketService()
            : base(new TestRpcSocket(), new TestNotifier())
        {
        }

        public new EnduranceEvent? Event
        {
            get => base.Event;
            set
            {
                var property = typeof(JudgeSocketService).GetProperty(nameof(Event));
                property!.SetValue(this, value);
            }
        }
    }

    sealed class TestRpcSocket : IRpcSocket
    {
        public event EventHandler<RpcError>? Error;
        public event EventHandler<SocketConnectionStatus>? ServerConnectionChanged;
        public event EventHandler<string>? ServerConnectionInfo;
        public Microsoft.AspNetCore.SignalR.Client.HubConnection? Connection => null;
        public bool IsConnected => false;

        public Task Connect(string groupId)
        {
            return Task.CompletedTask;
        }

        public Task Connect()
        {
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            return Task.CompletedTask;
        }

        public void RaiseError(Exception exception, string? procedure, params object?[] arguments)
        {
            Error?.Invoke(this, new RpcError(exception, procedure, arguments));
        }
    }

    sealed class TestNotifier : Not.Notify.INotifier, Not.Notify.INotificationStream
    {
        public Not.Events.IEventSubscriber<string> Informed { get; } = new Event<string>();
        public Not.Events.IEventSubscriber<string> Succeeded { get; } = new Event<string>();
        public Not.Events.IEventSubscriber<string> Warned { get; } = new Event<string>();
        public Not.Events.IEventSubscriber<string> Failed { get; } = new Event<string>();
        public Not.Events.IEventSubscriber<Exception> UnhandledExceptions { get; } = new Event<Exception>();

        public void Error(Exception exception) { }
        public void Error(string message) { }
        public void Inform(string message) { }
        public void Success(string message) { }
        public void Warn(string message) { }
    }
}
