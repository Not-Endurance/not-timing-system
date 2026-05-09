using Not.Events;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features;
using NTS.Judge.Contracts.Features.Core;

namespace NTS.Judge.Tests.Blazor;

public class ConfigureEventsListBehindTests
{
    [Fact]
    public void ShouldShowStart_WhenThereAreNoActiveEvents_ReturnsTrue()
    {
        var component = CreateComponent(activeEventInformationCount: 0);

        Assert.True(component.ShowStart(CreateConfigureEvent(7)));
    }

    [Fact]
    public void ShouldShowStart_WhenThereAreActiveEvents_ReturnsFalse()
    {
        var component = CreateComponent(activeEventInformationCount: 1);

        Assert.False(component.ShowStart(CreateConfigureEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventMatches_ReturnsTrue()
    {
        var component = CreateComponent(connectedEventId: 7);

        Assert.True(component.ShowReset(CreateConfigureEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventDiffers_ReturnsFalse()
    {
        var component = CreateComponent(connectedEventId: 8);

        Assert.False(component.ShowReset(CreateConfigureEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenNoConnectedEvent_ReturnsFalse()
    {
        var component = CreateComponent();

        Assert.False(component.ShowReset(CreateConfigureEvent(7)));
    }

    static TestConfigureEventsListBehind CreateComponent(int activeEventInformationCount = 0, int? connectedEventId = null)
    {
        var component = new TestConfigureEventsListBehind
        {
            Service = new TestDashService(),
            ActiveEventService = new TestEventInformationService(
                activeEventInformationCount > 0 ? [connectedEventId ?? 7] : []
            ),
            SocketService = new TestSocketService
            {
                Event = connectedEventId == null ? null : CreateEventInformation(connectedEventId.Value),
            },
        };
        return component;
    }

    static ConfigureEvent CreateConfigureEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new ConfigureEvent("Event", "Sofia", country, null, null, null, [], [], [], [], id);
    }

    static EventInformation CreateEventInformation(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
            country,
            "Event",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class TestConfigureEventsListBehind : HomeContentBehind
    {
        public IDashService Service
        {
            set => base.DashService = value;
        }

        public new INtsSocketService SocketService
        {
            set => base.SocketService = value;
        }

        public IActiveEventsContext ActiveEventService
        {
            set => base.ActiveEventContext = value;
        }

        public bool ShowStart(ConfigureEvent configureEvent)
        {
            return ShowStartButton(configureEvent);
        }

        public bool ShowReset(ConfigureEvent configureEvent)
        {
            return ShowResetTimingButton(configureEvent);
        }
    }

    sealed class TestDashService : IDashService
    {
        public Task<Result<IReadOnlyList<NTS.Domain.Setup.Services.StartValidation.StartValidationIssue>>> Validate(
            int configureEventId
        )
        {
            return Task.FromResult(
                Result.Success<IReadOnlyList<NTS.Domain.Setup.Services.StartValidation.StartValidationIssue>>([])
            );
        }

        public Task Start(int configureEventId)
        {
            return Task.CompletedTask;
        }

        public Task Deactivate()
        {
            return Task.CompletedTask;
        }

        public Task Reset()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestEventInformationService : IActiveEventsContext
    {
        readonly IReadOnlySet<int> _activeEventIds;

        public TestEventInformationService(IEnumerable<int> activeEventIds)
        {
            _activeEventIds = activeEventIds.ToHashSet();
        }

        public IEventSubscriber ObservableEvent { get; } = new Event();

        public bool IsActive(ConfigureEvent configureEvent)
        {
            return _activeEventIds.Contains(configureEvent.Id);
        }

        public void Add(EventInformation eventInformation) { }

        public void Remove(int eventId) { }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public Not.Application.RPC.SocketConnectionStatus Status { get; set; }
        public EventInformation? Event { get; set; }

        public Task Connect(EventInformation eventInformation)
        {
            Event = eventInformation;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            Event = null;
            IsConnected = false;
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
}
