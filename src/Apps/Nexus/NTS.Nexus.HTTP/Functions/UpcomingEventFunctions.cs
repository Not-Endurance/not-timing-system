using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public class UpcomingEventFunctions : FunctionBase
{
    readonly IRepository<UpcomingEventModel> _upcomingEvents;

    public UpcomingEventFunctions(
        IRepository<UpcomingEventModel> upcomingEventRepository,
        IFunctionLogger<UpcomingEventFunctions> logger
    )
        : base(logger)
    {
        _upcomingEvents = upcomingEventRepository;
    }

    [Function("upcoming-event-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upcoming-event")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var upcomingEvent = await ReadBody<UpcomingEvent>(request);
        if (upcomingEvent == null)
        {
            return UnexpectedPayload<UpcomingEvent>();
        }

        var document = UpcomingEventModel.MapFrom(upcomingEvent);
        await _upcomingEvents.Create(document);
        return new OkObjectResult($"Upcoming event {upcomingEvent.Place} stored successfully.");
    }

    [Function("upcoming-event-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(List));

        var documents = await _upcomingEvents.ReadMany();
        var result = documents.Select(x => x.MapToDomain());
        return new OkObjectResult(result);
    }

    [Function("upcoming-event-query-by-id")]
    public async Task<IActionResult> QueryById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(QueryById));

        var document = await _upcomingEvents.Read(id);
        if (document == null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(document.MapToDomain());
    }

    [Function("upcoming-event-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "upcoming-event")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var upcomingEvent = await ReadBody<UpcomingEvent>(request);
        if (upcomingEvent == null)
        {
            return UnexpectedPayload<UpcomingEvent>();
        }

        var document = UpcomingEventModel.MapFrom(upcomingEvent);
        await _upcomingEvents.Update(document);
        return new OkObjectResult($"Updated upcoming event {upcomingEvent.Place}");
    }

    [Function("upcoming-event-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var upcomingEvent = await _upcomingEvents.Read(id);
        if (upcomingEvent == null)
        {
            return new OkObjectResult($"Event with id '{id}' did not exist");
        }

        await _upcomingEvents.Delete(upcomingEvent);
        return new OkObjectResult($"Deleted upcoming event with id '{id}'");
    }
}
