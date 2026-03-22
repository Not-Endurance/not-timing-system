using Not.Events;
using Not.Structures;
using NTS.Application.Core;
using NTS.Application.Socket;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Features;
using NTS.Judge.Features.Core;

namespace NTS.Judge.Tests.Blazor;

public class UpcomingEventsListBehindTests
{
    [Fact]
    public void ShouldShowStart_WhenThereAreNoActiveEvents_ReturnsTrue()
    {
        var component = CreateComponent(activeEnduranceEventCount: 0);

        Assert.True(component.ShowStart(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowStart_WhenThereAreActiveEvents_ReturnsFalse()
    {
        var component = CreateComponent(activeEnduranceEventCount: 1);

        Assert.False(component.ShowStart(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventMatches_ReturnsTrue()
    {
        var component = CreateComponent(connectedEventId: 7);

        Assert.True(component.ShowReset(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenConnectedEventDiffers_ReturnsFalse()
    {
        var component = CreateComponent(connectedEventId: 8);

        Assert.False(component.ShowReset(CreateUpcomingEvent(7)));
    }

    [Fact]
    public void ShouldShowResetTiming_WhenNoConnectedEvent_ReturnsFalse()
    {
        var component = CreateComponent();

        Assert.False(component.ShowReset(CreateUpcomingEvent(7)));
    }

    static TestUpcomingEventsListBehind CreateComponent(int activeEnduranceEventCount = 0, int? connectedEventId = null)
    {
        var component = new TestUpcomingEventsListBehind
        {
            Service = new TestDashService(),
            ActiveEventService = new TestEnduranceEventService(
                activeEnduranceEventCount > 0 ? [connectedEventId ?? 7] : []
            ),
            SocketService = new TestSocketService
            {
                Event = connectedEventId == null ? null : CreateEnduranceEvent(connectedEventId.Value),
            },
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
        public IDashService Service
        {
            set => base.DashService = value;
        }

        public new INtsSocketService SocketService
        {
            set => base.SocketService = value;
        }

        public new IEnduranceEventService ActiveEventService
        {
            set => base.ActiveEventService = value;
        }

        public bool ShowStart(UpcomingEvent upcomingEvent)
        {
            return ShowStartButton(upcomingEvent);
        }

        public bool ShowReset(UpcomingEvent upcomingEvent)
        {
            return ShowResetTimingButton(upcomingEvent);
        }
    }

    sealed class TestDashService : IDashService
    {
        public Task<Result<IReadOnlyList<NTS.Domain.Setup.Services.StartValidation.StartValidationIssue>>> Validate(
            int upcomingEventId
        )
        {
            return Task.FromResult(
                Result.Success<IReadOnlyList<NTS.Domain.Setup.Services.StartValidation.StartValidationIssue>>([])
            );
        }

        public Task Start(int upcomingEventId)
        {
            return Task.CompletedTask;
        }

        public Task LoadArchive(int archiveId)
        {
            return Task.CompletedTask;
        }

        public Task Reset()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestEnduranceEventService : IEnduranceEventService
    {
        readonly IReadOnlySet<int> _activeEventIds;

        public TestEnduranceEventService(IEnumerable<int> activeEventIds)
        {
            _activeEventIds = activeEventIds.ToHashSet();
        }

        public IEventSubscriber ObservableEvent { get; } = new Event();

        public bool IsActive(UpcomingEvent upcomingEvent)
        {
            return _activeEventIds.Contains(upcomingEvent.Id);
        }

        public Task<IEnumerable<EnduranceEvent>> GetEvents()
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>([]);
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public Not.Application.RPC.SignalR.SocketConnectionStatus Status { get; set; }
        public EnduranceEvent? Event { get; set; }

        public Task Connect(EnduranceEvent enduranceEvent)
        {
            Event = enduranceEvent;
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            Event = null;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }
}
