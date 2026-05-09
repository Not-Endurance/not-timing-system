using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Structures;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Event;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Authentication.Tests;

public class EventInformationResetTests
{
    [Fact]
    public async Task Reset_service_deletes_all_repository_records_for_requested_event()
    {
        var repositories = new IEventResetRepository[]
        {
            new RecordingEventResetRepository(),
            new RecordingEventResetRepository(),
            new RecordingEventResetRepository(),
        };
        var service = new EventInformationResetService(repositories);

        await service.Reset(17);

        Assert.All(
            repositories.Cast<RecordingEventResetRepository>(),
            repository =>
            {
                var deletedEventId = Assert.Single(repository.DeleteAllForEventCalls);
                Assert.Equal(17, deletedEventId);
            }
        );
    }

    [Fact]
    public async Task Reset_function_invokes_reset_service_for_requested_event()
    {
        var resetService = new RecordingEventInformationResetService();
        var function = new EventInformationFunctions(
            new TestFunctionLogger(),
            new RecordingRepository<EventInformationModel>(),
            resetService,
            new RecordingEventInformationBusinessService(),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Delete, "/api/event-information/19/reset");

        var response = await function.Reset(request, 19);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.IsType<Not.Structures.Result>(ok.Value);
        Assert.Equal(19, resetService.LastEventId);
    }

    [Fact]
    public async Task Active_function_returns_events_from_business_service()
    {
        var activeEvent = CreateEventInformation(1, DateTimeOffset.UtcNow.AddDays(1), isActive: true);
        var businessService = new RecordingEventInformationBusinessService { ActiveEvents = [activeEvent] };
        var function = CreateFunction(new RecordingRepository<EventInformationModel>(), businessService);
        var request = CreateRequest(HttpMethods.Get, "/api/event-information/active");

        var response = await function.ListActive(request);

        var data = ReadPayload<IEnumerable<EventInformationModel>>(response).ToList();
        var returnedEvent = Assert.Single(data);
        Assert.Equal(activeEvent.Id, returnedEvent.Id);
        Assert.Equal(1, businessService.ReadActiveCalls);
    }

    [Fact]
    public async Task Past_function_returns_events_from_business_service()
    {
        var inactiveEvent = CreateEventInformation(2, DateTimeOffset.UtcNow.AddDays(1), isActive: false);
        var expiredInactiveEvent = CreateEventInformation(3, DateTimeOffset.UtcNow.AddDays(-1), isActive: false);
        var businessService = new RecordingEventInformationBusinessService
        {
            PastEvents = [inactiveEvent, expiredInactiveEvent],
        };
        var function = CreateFunction(new RecordingRepository<EventInformationModel>(), businessService);
        var request = CreateRequest(HttpMethods.Get, "/api/event-information/past");

        var response = await function.ListPast(request);

        var data = ReadPayload<IEnumerable<EventInformationModel>>(response).ToList();
        Assert.Equal([2, 3], data.Select(x => x.Id));
        Assert.Equal(1, businessService.ReadPastCalls);
    }

    [Fact]
    public async Task Deactivate_function_invokes_business_service_for_requested_event()
    {
        var businessService = new RecordingEventInformationBusinessService();
        var function = CreateFunction(new RecordingRepository<EventInformationModel>(), businessService);
        var request = CreateRequest(HttpMethods.Post, "/api/event-information/19/deactivate");

        var response = await function.Deactivate(request, 19);

        Assert.IsType<Result>(Assert.IsType<OkObjectResult>(response).Value);
        Assert.Equal([19], businessService.DeactivatedEventIds);
    }

    [Fact]
    public void Event_scoped_mongo_repositories_are_registered_for_event_reset()
    {
        var repositories = typeof(EventScopedMongoRepository<>)
            .Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && IsEventScopedMongoRepository(type));

        Assert.NotEmpty(repositories);
        Assert.All(
            repositories,
            repository => Assert.Contains(typeof(IEventResetRepository), repository.GetInterfaces())
        );
    }

    static HttpRequest CreateRequest(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context.Request;
    }

    static EventInformationFunctions CreateFunction(
        RecordingRepository<EventInformationModel> events,
        RecordingEventInformationBusinessService? businessService = null
    )
    {
        return new EventInformationFunctions(
            new TestFunctionLogger(),
            events,
            new RecordingEventInformationResetService(),
            businessService ?? new RecordingEventInformationBusinessService(),
            new TestTelemetryService()
        );
    }

    static EventInformationModel CreateEventInformation(int id, DateTimeOffset endDay, bool isActive)
    {
        return new EventInformationModel
        {
            Id = id,
            Name = $"Event {id}",
            Location = "Sofia",
            StartDay = endDay.AddDays(-1),
            EndDay = endDay,
            IsActive = isActive,
        };
    }

    static TPayload ReadPayload<TPayload>(IActionResult response)
    {
        var ok = Assert.IsType<OkObjectResult>(response);
        var result = Assert.IsType<Result<TPayload>>(ok.Value);
        return Assert.IsAssignableFrom<TPayload>(result.Data);
    }

    static bool IsEventScopedMongoRepository(Type type)
    {
        while (type != typeof(object) && type.BaseType != null)
        {
            type = type.BaseType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventScopedMongoRepository<>))
            {
                return true;
            }
        }

        return false;
    }

    sealed class RecordingEventResetRepository : IEventResetRepository
    {
        public List<int> DeleteAllForEventCalls { get; } = [];

        public Task DeleteAllForEvent(int eventId)
        {
            DeleteAllForEventCalls.Add(eventId);
            return Task.CompletedTask;
        }
    }

    sealed class RecordingEventInformationResetService : IEventInformationResetService
    {
        public int? LastEventId { get; private set; }

        public Task Reset(int eventId)
        {
            LastEventId = eventId;
            return Task.CompletedTask;
        }
    }

    sealed class RecordingEventInformationBusinessService : IEventInformationBusinessService
    {
        public IEnumerable<EventInformationModel> ActiveEvents { get; init; } = [];
        public IEnumerable<EventInformationModel> PastEvents { get; init; } = [];
        public int ReadActiveCalls { get; private set; }
        public int ReadPastCalls { get; private set; }
        public List<int> DeactivatedEventIds { get; } = [];

        public Task<IEnumerable<EventInformationModel>> ReadActive()
        {
            ReadActiveCalls++;
            return Task.FromResult(ActiveEvents);
        }

        public Task<IEnumerable<EventInformationModel>> ReadPast()
        {
            ReadPastCalls++;
            return Task.FromResult(PastEvents);
        }

        public Task<EventInformationModel> Start(int configureEventId)
        {
            return Task.FromResult(new EventInformationModel());
        }

        public Task Deactivate(int eventInformationId)
        {
            DeactivatedEventIds.Add(eventInformationId);
            return Task.CompletedTask;
        }
    }

    sealed class RecordingRepository<T> : Not.Application.CRUD.Ports.IRepository<T>
        where T : class
    {
        public RecordingRepository(IEnumerable<T>? items = null)
        {
            Items = items?.ToList() ?? [];
        }

        public List<T> Items { get; }
        public List<T> UpdatedItems { get; } = [];

        public Task Create(T item)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<T?> Read(int id)
        {
            return Task.FromResult(Items.FirstOrDefault(x => GetId(x) == id));
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult(Items.FirstOrDefault(filter.Compile()));
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>(Items);
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult<IEnumerable<T>>(Items.Where(filter.Compile()).ToList());
        }

        public Task Update(T item)
        {
            UpdatedItems.Add(item);
            var id = GetId(item);
            var index = Items.FindIndex(x => GetId(x) == id);
            if (index >= 0)
            {
                Items[index] = item;
            }
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            Items.Remove(item);
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            var itemSet = items.ToHashSet();
            Items.RemoveAll(itemSet.Contains);
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            var predicate = filter.Compile();
            Items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        static int? GetId(T item)
        {
            return item.GetType().GetProperty("Id")?.GetValue(item) as int?;
        }
    }

    sealed class TestFunctionLogger : IFunctionLogger<EventInformationFunctions>
    {
        public void LogDebug(HttpRequest request, string method = "") { }

        public void LogDebug(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogInformation(HttpRequest request, string method = "") { }

        public void LogInformation(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, string method = "") { }

        public void LogError(string template, HttpRequest request, object[] args, string method = "") { }

        public void LogError(HttpRequest request, Exception exception, string method = "") { }
    }

    sealed class TestTelemetryService : ITelemetryService
    {
        public Activity? StartActivity(string className, string methodName)
        {
            return null;
        }
    }
}
