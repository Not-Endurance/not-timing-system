using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.RPC;
using Not.Domain.Abstractions;
using Not.Events;
using Not.Notify;
using Not.Structures;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Contracts.Socket;
using NTS.Application.Core;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Services.StartValidation;
using NTS.Judge.Contracts.Features.Core;
using NTS.Judge.Contracts.Features.Setup.UpcomingEvents;
using NTS.Judge.Features.Core;
using NTS.Judge.Tests.Core.Implementations;

namespace NTS.Judge.Tests.Core;

public class DashServiceTests
{
    [Fact]
    public async Task Start_WhenEventIsStarted_StartsInRepositoryBeforeConnecting()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls);
        var startBusiness = new TestUpcomingEventService(calls);
        var enduranceEvents = new TestEnduranceEventRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, enduranceEvents);

        await service.Start(7);

        Assert.Equal(["Validate(7)", "Start(7)", "AddActive(7)", "Connect(7)"], calls);
    }

    [Fact]
    public async Task Start_WhenAlreadyConnected_DisconnectsBeforeStartingAndReconnects()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls) { IsConnected = true, Event = CreateEvent(3) };
        var startBusiness = new TestUpcomingEventService(calls);
        var enduranceEvents = new TestEnduranceEventRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, enduranceEvents);

        await service.Start(9);

        Assert.Equal(["Validate(9)", "Disconnect()", "Start(9)", "AddActive(9)", "Connect(9)"], calls);
    }

    [Fact]
    public async Task Reset_WhenSocketHasEvent_RemovesItFromActiveCache()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls) { Event = CreateEvent(11) };
        var startBusiness = new TestUpcomingEventService(calls);
        var enduranceEvents = new TestEnduranceEventRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, enduranceEvents);

        await service.Reset();

        Assert.Equal(["Reset()", "RemoveActive(11)"], calls);
    }

    static DashService CreateService(
        INtsSocketService socketService,
        IActiveEventsContext activeEventService,
        IUpcomingEventService upcomingEventService,
        IEnduranceEventRepository enduranceEvents
    )
    {
        return new DashService(
            socketService,
            activeEventService,
            [],
            enduranceEvents,
            upcomingEventService
        );
    }

    static EnduranceEvent CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EnduranceEvent(
            country,
            "Sofia",
            "Sofia",
            new EventSpan(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)),
            null,
            null,
            null,
            id
        );
    }

    sealed class TestUpcomingEventService : IUpcomingEventService
    {
        readonly List<string> _calls;

        public TestUpcomingEventService(List<string> calls)
        {
            _calls = calls;
        }

        public Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int upcomingEventId)
        {
            _calls.Add($"Validate({upcomingEventId})");
            return Task.FromResult(Result.Success<IReadOnlyList<StartValidationIssue>>([]));
        }

        public Task DeleteParticipation(int upcomingEventId, int participationNumber, int competitionId)
        {
            throw new NotImplementedException();
        }
    }

    sealed class TestActiveEventService : IActiveEventsContext
    {
        readonly List<string> _calls;

        public TestActiveEventService(List<string> calls)
        {
            _calls = calls;
        }

        public IEventSubscriber ObservableEvent { get; } = new Event();

        public bool IsActive(UpcomingEvent upcomingEvent)
        {
            return false;
        }

        public void Add(EnduranceEvent enduranceEvent)
        {
            _calls.Add($"AddActive({enduranceEvent.Id})");
        }

        public void Remove(int eventId)
        {
            _calls.Add($"RemoveActive({eventId})");
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestSocketService : INtsSocketService
    {
        readonly List<string> _calls;

        public TestSocketService(List<string> calls)
        {
            _calls = calls;
        }

        public IEventSubscriber ObservableEvent { get; } = new Event();
        public bool IsConnected { get; set; }
        public SocketConnectionStatus Status { get; set; } = SocketConnectionStatus.Disconnected;
        public EnduranceEvent? Event { get; set; }

        public Task Connect(EnduranceEvent enduranceEvent)
        {
            _calls.Add($"Connect({enduranceEvent.Id})");
            IsConnected = true;
            Event = enduranceEvent;
            Status = SocketConnectionStatus.Connected;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            _calls.Add("Disconnect()");
            IsConnected = false;
            Event = null;
            Status = SocketConnectionStatus.Disconnected;
            return Task.CompletedTask;
        }

        public Task<bool> WillResetSession(EnduranceEvent enduranceEvent)
        {
            return Task.FromResult(false);
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestEnduranceEventRepository : IEnduranceEventRepository
    {
        readonly List<string>? _calls;

        public TestEnduranceEventRepository(List<string>? calls = null)
        {
            _calls = calls;
        }

        public Task<EnduranceEvent> Start(int upcomingEventId)
        {
            _calls?.Add($"Start({upcomingEventId})");
            return Task.FromResult(CreateEvent(upcomingEventId));
        }

        public Task Reset()
        {
            _calls?.Add("Reset()");
            return Task.CompletedTask;
        }

        public Task Create(EnduranceEvent item)
        {
            return Task.CompletedTask;
        }

        public Task<EnduranceEvent?> Read(Expression<Func<EnduranceEvent, bool>> filter)
        {
            return Task.FromResult<EnduranceEvent?>(null);
        }

        public Task<EnduranceEvent?> Read(int id)
        {
            return Task.FromResult<EnduranceEvent?>(null);
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany()
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>([]);
        }

        public Task<IEnumerable<EnduranceEvent>> ReadMany(Expression<Func<EnduranceEvent, bool>> filter)
        {
            return Task.FromResult<IEnumerable<EnduranceEvent>>([]);
        }

        public Task Update(EnduranceEvent item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(EnduranceEvent item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            return Task.CompletedTask;
        }

        public Task Delete(Expression<Func<EnduranceEvent, bool>> filter)
        {
            return Task.CompletedTask;
        }

        public Task Delete(IEnumerable<EnduranceEvent> items)
        {
            return Task.CompletedTask;
        }
    }

}
