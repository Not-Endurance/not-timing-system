using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Event;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Authentication.Tests;

public class EnduranceEventResetTests
{
    [Fact]
    public async Task Reset_service_uses_version_one_when_event_has_no_deleted_documents()
    {
        var repositories = new[] { new RecordingEventResetRepository(), new RecordingEventResetRepository() };
        var service = new EnduranceEventResetService(repositories);

        await service.Reset(17);

        Assert.All(
            repositories,
            repository =>
            {
                var call = Assert.Single(repository.SoftDeleteCalls);
                Assert.Equal(17, call.EventId);
                Assert.Equal(1, call.DeletedVersion);
            }
        );
    }

    [Fact]
    public async Task Reset_service_increments_the_highest_deleted_version_found()
    {
        var repositories = new IEventResetRepository[]
        {
            new RecordingEventResetRepository { MaxDeletedVersion = 2 },
            new RecordingEventResetRepository { MaxDeletedVersion = 5 },
            new RecordingEventResetRepository(),
        };
        var service = new EnduranceEventResetService(repositories);

        await service.Reset(23);

        Assert.All(
            repositories.Cast<RecordingEventResetRepository>(),
            repository =>
            {
                var call = Assert.Single(repository.SoftDeleteCalls);
                Assert.Equal(23, call.EventId);
                Assert.Equal(6, call.DeletedVersion);
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
            new TestTelemetryService()
        );
        var request = CreateRequest(HttpMethods.Delete, "/api/endurance-event/19/reset");

        var response = await function.Reset(request, 19);

        Assert.IsType<OkResult>(response);
        Assert.Equal(19, resetService.LastEventId);
    }

    static HttpRequest CreateRequest(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context.Request;
    }

    sealed class RecordingEventResetRepository : IEventResetRepository
    {
        public int? MaxDeletedVersion { get; init; }
        public List<(int EventId, int DeletedVersion)> SoftDeleteCalls { get; } = [];

        public Task<int?> GetMaxDeletedVersion(int eventId)
        {
            return Task.FromResult(MaxDeletedVersion);
        }

        public Task SoftDeleteActive(int eventId, int deletedVersion)
        {
            SoftDeleteCalls.Add((eventId, deletedVersion));
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

    sealed class RecordingRepository<T> : Not.Application.CRUD.Ports.IRepository<T>
    {
        public Task Create(T item) => Task.CompletedTask;

        public Task<T?> Read(int id) => Task.FromResult(default(T));

        public Task<T?> Read(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));

        public Task<IEnumerable<T>> ReadMany() => Task.FromResult<IEnumerable<T>>([]);

        public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter) => Task.FromResult<IEnumerable<T>>([]);

        public Task Update(T item) => Task.CompletedTask;

        public Task Delete(T item) => Task.CompletedTask;

        public Task Delete(IEnumerable<T> items) => Task.CompletedTask;

        public Task Delete(Expression<Func<T, bool>> filter) => Task.CompletedTask;
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
