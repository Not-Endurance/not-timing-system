using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class UpcomingEventFunctions : FunctionBase
{
    readonly IRepository<UpcomingEventModel> _upcomingEvents;

    public UpcomingEventFunctions(
        IRepository<UpcomingEventModel> upcomingEventRepository,
        IFunctionLogger<UpcomingEventFunctions> logger,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _upcomingEvents = upcomingEventRepository;
    }

    [Function("upcoming-event-insert")]
    public Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upcoming-event")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Insert), async () =>
        {
            var upcomingEvent = await ReadBody<UpcomingEvent>(request);
            if (upcomingEvent == null)
            {
                return UnexpectedPayload<UpcomingEvent>();
            }

            var document = UpcomingEventModel.MapFrom(upcomingEvent);
            await ExecuteWithTelemetry("RepositoryCreate", () => _upcomingEvents.Create(document));
            return new OkObjectResult($"Upcoming event {upcomingEvent.Place} stored successfully.");
        });
    }

    [Function("upcoming-event-list")]
    public Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(List), async () =>
        {
            var documents = await ExecuteWithTelemetry("RepositoryReadMany", () => _upcomingEvents.ReadMany());
            var result = documents.Select(x => x.MapToDomain());
            return new OkObjectResult(result);
        });
    }

    [Function("upcoming-event-query-by-id")]
    public Task<IActionResult> QueryById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(QueryById), async () =>
        {
            var document = await ExecuteWithTelemetry("RepositoryRead", () => _upcomingEvents.Read(id));
            if (document == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(document.MapToDomain());
        });
    }

    [Function("upcoming-event-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "upcoming-event")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Update), async () =>
        {
            var upcomingEvent = await ReadBody<UpcomingEvent>(request);
            if (upcomingEvent == null)
            {
                return UnexpectedPayload<UpcomingEvent>();
            }

            var document = UpcomingEventModel.MapFrom(upcomingEvent);
            await ExecuteWithTelemetry("RepositoryUpdate", () => _upcomingEvents.Update(document));
            return new OkObjectResult($"Updated upcoming event {upcomingEvent.Place}");
        });
    }

    [Function("upcoming-event-delete")]
    public Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(Delete), async () =>
        {
            var upcomingEvent = await ExecuteWithTelemetry("RepositoryRead", () => _upcomingEvents.Read(id));
            if (upcomingEvent == null)
            {
                return new OkObjectResult($"Event with id '{id}' did not exist");
            }

            await ExecuteWithTelemetry("RepositoryDelete", () => _upcomingEvents.Delete(upcomingEvent));
            return new OkObjectResult($"Deleted upcoming event with id '{id}'");
        });
    }
}
