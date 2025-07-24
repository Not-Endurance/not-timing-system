using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Serialization.JSON;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions.UpcomingEvents;

public class UpcomingEventFunctions : FunctionBase<UpcomingEventFunctions>
{
    readonly IUpcomingEventRepository _upcomingEventRepository;

    public UpcomingEventFunctions(
        IUpcomingEventRepository upcomingEventRepository,
        IFunctionLogger<UpcomingEventFunctions> logger
    )
        : base(logger)
    {
        _upcomingEventRepository = upcomingEventRepository;
    }

    [Function("upcoming-event-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upcoming-event")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var upcomingEvent = requestBody.FromJson<UpcomingEvent>();
        var document = UpcomingEventDocument.Create(upcomingEvent);
        await _upcomingEventRepository.Create(document);

        return new OkObjectResult($"Upcoming event {upcomingEvent.Place} stored successfully.");
    }

    [Function("upcoming-event-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event")] HttpRequest request
    )
    {
        LogInformation(request);

        var documents = await _upcomingEventRepository.ReadAll();
        var result = documents.Select(x => x.ToDomain());
        return new OkObjectResult(result);
    }

    [Function("upcoming-event-query-by-id")]
    public async Task<IActionResult> QueryById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var document = await _upcomingEventRepository.Read(id);
        if (document == null)
        {
            return new NotFoundResult();
        }

        return new OkObjectResult(document.ToDomain());
    }

    [Function("upcoming-event-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "upcoming-event")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var upcomingEvent = requestBody.FromJson<UpcomingEvent>();
        var document = UpcomingEventDocument.Create(upcomingEvent);
        await _upcomingEventRepository.Update(document);

        return new OkObjectResult($"Updated upcoming event {upcomingEvent.Place}");
    }

    [Function("upcoming-event-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "upcoming-event/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        await _upcomingEventRepository.Delete(id);
        return new OkObjectResult($"Deleted upcoming event with id '{id}'");
    }
}
