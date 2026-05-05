using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NTS.Application.Contracts.Core.Models;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Event;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Authentication.Tests;

public class EnduranceEventResetTests
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
        var service = new EnduranceEventResetService(repositories);

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
        var resetService = new RecordingEnduranceEventResetService();
        var function = new EnduranceEventFunctions(
            new TestFunctionLogger(),
            new RecordingRepository<EnduranceEventModel>(),
            resetService,
            new RecordingEnduranceEventBusinessService(),
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Delete, "/api/endurance-event/19/reset");

        var response = await function.Reset(request, 19);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.IsType<Not.Structures.Result>(ok.Value);
        Assert.Equal(19, resetService.LastEventId);
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

    sealed class RecordingEnduranceEventResetService : IEnduranceEventResetService
    {
        public int? LastEventId { get; private set; }

        public Task Reset(int eventId)
        {
            LastEventId = eventId;
            return Task.CompletedTask;
        }
    }

    sealed class RecordingEnduranceEventBusinessService : IEnduranceEventBusinessService
    {
        public Task<EnduranceEventModel> Start(int upcomingEventId)
        {
            return Task.FromResult(new EnduranceEventModel());
        }
    }

    sealed class RecordingRepository<T> : Not.Application.CRUD.Ports.IRepository<T>
    {
        public Task Create(T item)
        {
            return Task.CompletedTask;
        }

        public Task<T?> Read(int id)
        {
            return Task.FromResult(default(T));
        }

        public Task<T?> Read(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult(default(T));
        }

        public Task<IEnumerable<T>> ReadMany()
        {
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult<IEnumerable<T>>([]);
        }

        public Task Update(T item)
        {
            return Task.CompletedTask;
        }

        public Task Delete(T item)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<T> items)
        {
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<T, bool>> filter)
        {
            return Task.CompletedTask;
        }
    }

    sealed class TestFunctionLogger : IFunctionLogger<EnduranceEventFunctions>
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
