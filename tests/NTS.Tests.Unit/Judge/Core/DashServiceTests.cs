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
using NTS.Judge.Contracts.Features.Setup.ConfigureEvents;
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
        var startBusiness = new TestConfigureEventService(calls);
        var eventInformation = new TestEventInformationRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, eventInformation);

        await service.Start(7);

        Assert.Equal(["Validate(7)", "Start(7)", "AddActive(7)", "Connect(7)"], calls);
    }

    [Fact]
    public async Task Start_WhenAlreadyConnected_DisconnectsBeforeStartingAndReconnects()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls) { IsConnected = true, Event = CreateEvent(3) };
        var startBusiness = new TestConfigureEventService(calls);
        var eventInformation = new TestEventInformationRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, eventInformation);

        await service.Start(9);

        Assert.Equal(["Validate(9)", "Disconnect()", "Start(9)", "AddActive(9)", "Connect(9)"], calls);
    }

    [Fact]
    public async Task Reset_WhenSocketHasEvent_RemovesItFromActiveCache()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls) { Event = CreateEvent(11) };
        var startBusiness = new TestConfigureEventService(calls);
        var eventInformation = new TestEventInformationRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, eventInformation);

        await service.Reset();

        Assert.Equal(["Reset()", "RemoveActive(11)"], calls);
    }

    [Fact]
    public async Task Deactivate_WhenSocketHasEvent_DeactivatesAndRemovesItFromActiveCache()
    {
        var calls = new List<string>();
        var socketService = new TestSocketService(calls) { Event = CreateEvent(11) };
        var startBusiness = new TestConfigureEventService(calls);
        var eventInformation = new TestEventInformationRepository(calls);
        var activeEvents = new TestActiveEventService(calls);
        var service = CreateService(socketService, activeEvents, startBusiness, eventInformation);

        await service.Deactivate();

        Assert.Equal(["Deactivate()", "RemoveActive(11)"], calls);
    }

    static DashService CreateService(
        INtsSocketService socketService,
        IActiveEventsContext activeEventService,
        IConfigureEventService configureEventService,
        IEventInformationRepository eventInformation
    )
    {
        return new DashService(
            socketService,
            activeEventService,
            [],
            eventInformation,
            configureEventService
        );
    }

    static EventInformation CreateEvent(int id)
    {
        var country = new Country(1, "Bulgaria", "BG", "BUL", "bg-BG");
        return new EventInformation(
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

    sealed class TestConfigureEventService : IConfigureEventService
    {
        readonly List<string> _calls;

        public TestConfigureEventService(List<string> calls)
        {
            _calls = calls;
        }

        public Task<Result<IReadOnlyList<StartValidationIssue>>> Validate(int configureEventId)
        {
            _calls.Add($"Validate({configureEventId})");
            return Task.FromResult(Result.Success<IReadOnlyList<StartValidationIssue>>([]));
        }

        public Task DeleteParticipation(int configureEventId, int participationNumber, int competitionId)
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

        public bool IsActive(ConfigureEvent configureEvent)
        {
            return false;
        }

        public void Add(EventInformation eventInformation)
        {
            _calls.Add($"AddActive({eventInformation.Id})");
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
        public EventInformation? Event { get; set; }

        public Task Connect(EventInformation eventInformation)
        {
            _calls.Add($"Connect({eventInformation.Id})");
            IsConnected = true;
            Event = eventInformation;
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

        public Task<bool> WillResetSession(EventInformation eventInformation)
        {
            return Task.FromResult(false);
        }

        public Task Load()
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestEventInformationRepository : IEventInformationRepository
    {
        readonly List<string>? _calls;

        public TestEventInformationRepository(List<string>? calls = null)
        {
            _calls = calls;
        }

        public Task<EventInformation> Start(int configureEventId)
        {
            _calls?.Add($"Start({configureEventId})");
            return Task.FromResult(CreateEvent(configureEventId));
        }

        public Task Reset()
        {
            _calls?.Add("Reset()");
            return Task.CompletedTask;
        }

        public Task Deactivate()
        {
            _calls?.Add("Deactivate()");
            return Task.CompletedTask;
        }

        public Task<IEnumerable<EventInformation>> ReadActive()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task<IEnumerable<EventInformation>> ReadPast()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task Create(EventInformation item)
        {
            return Task.CompletedTask;
        }

        public Task<EventInformation?> Read(Expression<Func<EventInformation, bool>> filter)
        {
            return Task.FromResult<EventInformation?>(null);
        }

        public Task<EventInformation?> Read(int id)
        {
            return Task.FromResult<EventInformation?>(null);
        }

        public Task<IEnumerable<EventInformation>> ReadMany()
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task<IEnumerable<EventInformation>> ReadMany(Expression<Func<EventInformation, bool>> filter)
        {
            return Task.FromResult<IEnumerable<EventInformation>>([]);
        }

        public Task Update(EventInformation item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(EventInformation item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<EventInformation, bool>> filter)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<EventInformation> items)
        {
            return Task.CompletedTask;
        }
    }

}
